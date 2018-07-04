using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TheBulb;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The Bulb
/// Created by Timwi
/// </summary>
public class TheBulbModule : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;

    public Light Light1;
    public Light Light2;
    public MeshRenderer Glass;
    public GameObject Filament;

    public KMSelectable ButtonO;
    public KMSelectable Bulb;
    public KMSelectable ButtonI;

    public Transform OFace;
    public Transform IFace;

    enum BulbColor
    {
        Blue,
        Red,
        Green,
        Yellow,
        White,
        Purple
    }

    private Color[] _haloColors = Ext.NewArray(
        "6AA8FF",   // blue
        "FF0005",   // red
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    private Color[] _bulbColors = Ext.NewArray(
        "1B3E70",   // blue
        "FF0005",   // red
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    private BulbColor _bulbColor;
    private bool _opaque;
    private bool _initiallyOn;
    private bool _wentOffAtStep1;
    private bool _rememberedIndicatorPresent;
    private bool _isBulbUnscrewed;
    private bool _mustUndoBulbScrewing;
    private bool _pressedOAtStep1;
    private bool _wentOnBeforeStep12To15;
    private bool _wasOnAtUnscrew;
    private int _stage;
    private string _correctButtonPresses;
    private bool _isSolved;
    private bool _isButtonDown;
    private Coroutine _buttonDownCoroutine;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _isSolved = false;

        if (Rnd.Range(0, 2) == 0)
        {
            // Swap the buttons! Fun fun
            var p = IFace.localPosition;
            IFace.localPosition = OFace.localPosition;
            OFace.localPosition = p;
            var t = ButtonO;
            ButtonO = ButtonI;
            ButtonI = t;
        }

        var colorIndex = Rnd.Range(0, _bulbColors.Length);
        _bulbColor = (BulbColor) colorIndex;
        _opaque = Rnd.Range(0, 2) == 0;
        Filament.SetActive(!_opaque);
        _initiallyOn = Rnd.Range(0, 2) == 0;
        Glass.material.color = _bulbColors[colorIndex].WithAlpha(_opaque ? 1f : .55f);
        Light1.color = Light2.color = _haloColors[colorIndex].WithAlpha(_opaque ? 1f : .55f);
        _stage = -1;
        _isBulbUnscrewed = false;
        _correctButtonPresses = "";

        Debug.LogFormat("[The Bulb #{3}] Bulb is {0}, {1} and {2}.", _bulbColor, _opaque ? "opaque" : "see-through", _initiallyOn ? "on" : "off", _moduleId);

        ButtonO.OnInteract += delegate { ButtonO.AddInteractionPunch(); _buttonDownCoroutine = StartCoroutine(HandleLongPress(o: true)); return false; };
        ButtonI.OnInteract += delegate { ButtonI.AddInteractionPunch(); _buttonDownCoroutine = StartCoroutine(HandleLongPress(o: false)); return false; };
        ButtonO.OnInteractEnded += delegate { HandleButtonUp(o: true); };
        ButtonI.OnInteractEnded += delegate { HandleButtonUp(o: false); };
        Bulb.OnInteract += delegate { HandleBulb(); return false; };

        Module.OnActivate += delegate
        {
            if (_isBulbUnscrewed || _isScrewing)
                _wasOnAtUnscrew = _initiallyOn;
            else
                TurnLights(on: _initiallyOn);
            _stage = 1;
        };
    }

    private string stageToString(int stage)
    {
        if (stage >= 200)
            return "Screw back in; then " + stageToString(stage - 200);
        else if (stage >= 100)
            return "Unscrew; then " + stageToString(stage - 100);
        else if (stage == 0)
            return "you’re done";
        else
            return "Step " + stage;
    }

    private void TurnLights(bool on)
    {
        Light1.enabled = on;
        Light2.enabled = on;
    }

    private bool _isScrewing;

    private IEnumerator Screw(bool @in)
    {
        if (!@in)
        {
            _wasOnAtUnscrew = Light1.enabled;
            TurnLights(on: false);
        }

        var elapsed = 0f;
        const float totalAnimationTime = 1f;

        Audio.PlaySoundAtTransform(@in ? "ScrewIn" : "Unscrew", Bulb.transform);

        while (elapsed < totalAnimationTime)
        {
            yield return null;
            var delta = Time.deltaTime;
            elapsed += delta;
            Bulb.transform.Rotate(Vector3.up, (@in ? 1 : -1) * (540 * delta / totalAnimationTime));
            Bulb.transform.Translate(new Vector3(0, (@in ? -1 : 1) * (0.054f * delta / totalAnimationTime), 0));
        }
        _isScrewing = false;

        if (_isSolved)
            yield break;

        if (@in && (_wasOnAtUnscrew || _wentOnBeforeStep12To15))
            TurnLights(on: true);

        if (_stage == 0)
        {
            Debug.LogFormat("[The Bulb #{1}] Module solved. The correct button presses were: {0}", _correctButtonPresses, _moduleId);
            Module.HandlePass();
            StartCoroutine(victory());
        }
    }

    private void HandleBulb()
    {
        Bulb.AddInteractionPunch();
        if (_isScrewing)
            return;
        _isScrewing = true;
        StartCoroutine(Screw(_isBulbUnscrewed));
        _isBulbUnscrewed = !_isBulbUnscrewed;

        if (_isSolved)
            return;

        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[The Bulb #{1}] Undoing incorrect {0}.", _isBulbUnscrewed ? "screwing in" : "unscrewing", _moduleId);
            _mustUndoBulbScrewing = false;
            return;
        }

        var isCorrect = true;
        var extra = "";

        if (_stage >= 200)
        {
            _stage -= 200;
            if (_stage == 12 || _stage == 13)
            {
                _wentOnBeforeStep12To15 = (Rnd.Range(0, 2) == 0);
                extra = string.Format(" Bulb {0} at screw-in.", _wentOnBeforeStep12To15 ? "went on" : "did NOT go on");
            }
        }
        else if (_stage >= 100)
            _stage -= 100;
        else
        {
            isCorrect = false;

            switch (_stage)
            {
                case 1:
                    if (isCorrect = !_initiallyOn)
                        _stage = 4;
                    break;

                case 2:
                    if (isCorrect = (_bulbColor != BulbColor.Red && _bulbColor != BulbColor.White))
                        _stage = 7;
                    break;

                case 3:
                    if (isCorrect = (_bulbColor != BulbColor.Green && _bulbColor != BulbColor.Purple))
                        _stage = 8;
                    break;

                case 9:
                    isCorrect = (_bulbColor == BulbColor.Purple || _bulbColor == BulbColor.Red) && !_isBulbUnscrewed;
                    break;

                case 10:
                    isCorrect = (_bulbColor == BulbColor.Green || _bulbColor == BulbColor.White) && !_isBulbUnscrewed;
                    break;
            }
        }

        Debug.LogFormat("[The Bulb #{2}] {0}: {1}.", _isBulbUnscrewed ? "Unscrewing" : "Screwing in", isCorrect ? string.Format("CORRECT.{0} Stage is now: {1}", extra, stageToString(_stage)) : "WRONG", _moduleId);
        if (!isCorrect)
        {
            Module.HandleStrike();
            _mustUndoBulbScrewing = true;
        }
    }

    private IEnumerator HandleLongPress(bool o)
    {
        if (_isSolved)
            yield break;

        _isButtonDown = true;
        Audio.PlaySoundAtTransform("ButtonClick", (o ? ButtonO : ButtonI).transform);
        yield return new WaitForSeconds(.7f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, Bulb.transform);
        _isButtonDown = false;

        if (_isBulbUnscrewed)
        {
            Debug.LogFormat("[The Bulb #{0}] You tried to reset while the bulb was unscrewed. That is not allowed.", _moduleId);
            Module.HandleStrike();
            yield break;
        }

        TurnLights(on: _initiallyOn);
        _stage = 1;
        _mustUndoBulbScrewing = false;
        _correctButtonPresses = "";
        Debug.LogFormat("[The Bulb #{0}] Module reset.", _moduleId);
    }

    private void HandleButtonUp(bool o)
    {
        if (!_isButtonDown || _isSolved)
            return;
        if (_buttonDownCoroutine != null)
            StopCoroutine(_buttonDownCoroutine);
        _isButtonDown = false;

        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[The Bulb #{1}] The light bulb should have been {0} before pressing any more buttons.", _isBulbUnscrewed ? "screwed back in" : "unscrewed again", _moduleId);
            Module.HandleStrike();
            return;
        }

        var isCorrect = false;
        var extra = "";

        switch (_stage)
        {
            case 1:
                if (_initiallyOn && (_opaque ? o : !o))
                {
                    isCorrect = true;
                    if (_opaque
                        ? (_bulbColor == BulbColor.Green || _bulbColor == BulbColor.Purple)
                        : (_bulbColor == BulbColor.Red || _bulbColor == BulbColor.White))
                    {
                        TurnLights(on: !(_wentOffAtStep1 = (Rnd.Range(0, 2) == 0)));
                        Debug.LogFormat("[The Bulb #{1}] The light bulb {0} at Step 1.", _wentOffAtStep1 ? "went off" : "did not go off", _moduleId);
                    }
                    else
                        TurnLights(on: false);
                    _stage = o ? 3 : 2;
                    _pressedOAtStep1 = o;
                }
                break;

            case 2:
                if ((_bulbColor == BulbColor.Red && !o) || (_bulbColor == BulbColor.White && o))
                {
                    isCorrect = true;
                    TurnLights(on: false);
                    _stage = o ? 106 : 105;
                }
                break;

            case 3:
                if ((_bulbColor == BulbColor.Green && !o) || (_bulbColor == BulbColor.Purple && o))
                {
                    isCorrect = true;
                    TurnLights(on: false);
                    _stage = o ? 105 : 106;
                }
                break;

            case 4:
                if (isCorrect = (Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.CAR) ||
                    Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.IND) ||
                    Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.MSA) ||
                    Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.SND) ? !o : o))
                    _stage = o ? 10 : 9;
                break;

            case 5:
                if (isCorrect = (_wentOffAtStep1 ? (_pressedOAtStep1 == o) : (_pressedOAtStep1 != o)))
                    _stage = 200;
                break;

            case 6:
                if (isCorrect = ((_wentOffAtStep1 ^ _pressedOAtStep1) ? (_pressedOAtStep1 == o) : (_pressedOAtStep1 != o)))
                    _stage = 200;
                break;

            case 7:
                _rememberedIndicatorPresent = Bomb.IsIndicatorPresent(_bulbColor == BulbColor.Blue ? KMBombInfoExtensions.KnownIndicatorLabel.CLR : KMBombInfoExtensions.KnownIndicatorLabel.SIG);
                if (isCorrect = ((_bulbColor == BulbColor.Green) || (_bulbColor == BulbColor.Purple) ? !o : o))
                    _stage = (_bulbColor == BulbColor.Blue) || (_bulbColor == BulbColor.Green) ? 11 : (_bulbColor == BulbColor.Purple) ? 212 : 213;
                break;

            case 8:
                _rememberedIndicatorPresent = Bomb.IsIndicatorPresent(_bulbColor == BulbColor.White ? KMBombInfoExtensions.KnownIndicatorLabel.FRQ : KMBombInfoExtensions.KnownIndicatorLabel.FRK);
                if (isCorrect = ((_bulbColor == BulbColor.White) || (_bulbColor == BulbColor.Red) ? !o : o))
                    _stage = (_bulbColor == BulbColor.White) || (_bulbColor == BulbColor.Yellow) ? 11 : (_bulbColor == BulbColor.Red) ? 213 : 212;
                break;

            case 9:
                if (isCorrect = (
                    _bulbColor == BulbColor.Blue || _bulbColor == BulbColor.Green ? !o :
                    _bulbColor == BulbColor.Yellow || _bulbColor == BulbColor.White ? o :
                    _bulbColor == BulbColor.Purple ? !_isBulbUnscrewed && !o : !_isBulbUnscrewed && o))
                {
                    _stage =
                        _bulbColor == BulbColor.Blue ? 14 :
                        _bulbColor == BulbColor.Green ? 212 :
                        _bulbColor == BulbColor.Yellow ? 15 :
                        _bulbColor == BulbColor.White ? 213 :
                        _bulbColor == BulbColor.Purple ? 12 : 13;
                    if (_stage < 100 && !_isBulbUnscrewed)
                    {
                        _wentOnBeforeStep12To15 = (Rnd.Range(0, 2) == 0);
                        extra = string.Format(" Bulb {0} at {1} button press.", _wentOnBeforeStep12To15 ? "went on" : "did NOT go on", o ? "O" : "I");
                        TurnLights(on: _wentOnBeforeStep12To15);
                    }
                }
                break;

            case 10:
                if (isCorrect = (
                    _bulbColor == BulbColor.Purple || _bulbColor == BulbColor.Red ? !o :
                    _bulbColor == BulbColor.Blue || _bulbColor == BulbColor.Yellow ? o :
                    _bulbColor == BulbColor.Green ? !_isBulbUnscrewed && !o : !_isBulbUnscrewed && o))
                {
                    _stage =
                        _bulbColor == BulbColor.Purple ? 14 :
                        _bulbColor == BulbColor.Red ? 213 :
                        _bulbColor == BulbColor.Blue ? 15 :
                        _bulbColor == BulbColor.Yellow ? 212 :
                        _bulbColor == BulbColor.Green ? 13 : 12;
                    if (_stage < 100 && !_isBulbUnscrewed)
                    {
                        _wentOnBeforeStep12To15 = (Rnd.Range(0, 2) == 0);
                        extra = string.Format(" Bulb {0} at {1} button press.", _wentOnBeforeStep12To15 ? "went on" : "did NOT go on", o ? "O" : "I");
                        TurnLights(on: _wentOnBeforeStep12To15);
                    }
                }
                break;

            case 11:
                if (isCorrect = (_rememberedIndicatorPresent ? !o : o))
                    _stage = 200;
                break;

            case 12:
                if (isCorrect = (_wentOnBeforeStep12To15 ? !o : o))
                    _stage = 0;
                break;

            case 13:
                if (isCorrect = (_wentOnBeforeStep12To15 ? o : !o))
                    _stage = 0;
                break;

            case 14:
                if (isCorrect = (_opaque ? !o : o))
                    _stage = 200;
                break;

            case 15:
                if (isCorrect = (_opaque ? o : !o))
                    _stage = 200;
                break;
        }

        Debug.LogFormat("[The Bulb #{2}] Pressing {0}: {1}.", o ? "O" : "I", isCorrect ? string.Format("CORRECT.{0} Stage is now: {1}", extra, stageToString(_stage)) : "WRONG", _moduleId);
        if (!isCorrect)
            Module.HandleStrike();
        else
        {
            _correctButtonPresses += o ? "O" : "I";
            if (_stage == 0)
            {
                Debug.LogFormat("[The Bulb #{1}] Module solved. The correct button presses were: {0}", _correctButtonPresses, _moduleId);
                Module.HandlePass();
                StartCoroutine(victory());
            }
        }
    }

    private IEnumerator victory()
    {
        _isSolved = true;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);

        for (int i = 0; i < 5; i++)
        {
            TurnLights(on: true);
            yield return new WaitForSeconds(.2f);
            TurnLights(on: false);
            yield return new WaitForSeconds(.2f);
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Commands are “!{0} O”, “!{0} I”, “!{0} screw” and “!{0} unscrew”. Perform several commands with e.g. “!{0} O, unscrew, I, screw”. Reset the module with “!{0} reset”.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        var actions = new List<Func<object[]>>();

        foreach (var piece in Regex.Replace(command.ToLowerInvariant(), " +", " ").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (piece.Trim())
            {
                case "o":
                case "0":
                case "press o":
                case "press 0":
                    actions.Add(() => new object[] { ButtonO, new WaitForSeconds(.1f), ButtonO });
                    break;

                case "i":
                case "1":
                case "press i":
                case "press 1":
                    actions.Add(() => new object[] { ButtonI, new WaitForSeconds(.1f), ButtonI });
                    break;

                case "screw":
                case "screw in":
                case "screw it in":
                case "screwin":
                case "screwitin":
                    actions.Add(() => !_isBulbUnscrewed ? null : new object[] { Bulb, new WaitForSeconds(.1f), Bulb });
                    break;

                case "unscrew":
                    actions.Add(() => _isBulbUnscrewed ? null : new object[] { Bulb, new WaitForSeconds(.1f), Bulb });
                    break;

                case "reset":
                    actions.Add(() => new object[] { ButtonI, new WaitForSeconds(1f), ButtonI });
                    break;

                default:
                    yield break;
            }
        }

        yield return null;

        foreach (var action in actions)
        {
            var result = action();
            if (result == null)
                yield break;
            foreach (var obj in result)
            {
                yield return obj;
                if (_stage == 0)
                    yield return "solve";
                while (_isScrewing)
                {
                    yield return "trycancel";
                    yield return new WaitForSeconds(.1f);
                }
            }
            yield return new WaitForSeconds(.1f);
        }
    }
}
