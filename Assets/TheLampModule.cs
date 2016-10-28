using System;
using System.Linq;
using TheLamp;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The Lamp
/// Created by Timwi
/// </summary>
public class TheLampModule : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;

    public Light Light1;
    public Light Light2;
    public MeshRenderer Glass;

    public KMSelectable ButtonO;
    public KMSelectable Bulb;
    public KMSelectable ButtonI;

    public Transform OFace;
    public Transform IFace;

    enum LampColor
    {
        Blue,
        Red,
        Green,
        Yellow,
        White,
        Purple
    }

    private Color[] _lampColors = Ext.NewArray(
        "3A9DFF",   // blue
        "FF1E00",   // red
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Where(s => s != null)
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    private LampColor _lampColor;
    private bool _opaque;
    private bool _initiallyOn;
    private bool _wentOffAtStep1;
    private bool _rememberedIndicatorPresent;
    private bool _isBulbUnscrewed;
    private bool _mustUndoBulbScrewing;
    private bool _pressedOAtStep1;
    private bool _wentOnAtScrewIn;
    private int _stage;

    void Start()
    {
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

        var colorIndex = Rnd.Range(0, _lampColors.Length);
        _lampColor = (LampColor) colorIndex;
        _opaque = Rnd.Range(0, 2) == 0;
        _initiallyOn = Rnd.Range(0, 2) == 0;
        Glass.material.color = Light1.color = Light2.color = _lampColors[colorIndex].WithAlpha(_opaque ? 1f : .55f);
        _stage = 0;
        _isBulbUnscrewed = false;

        Debug.LogFormat("[TheLamp] Initial state: Color={0}, Opaque={1}, Initially on={2}", _lampColor, _opaque, _initiallyOn);

        ButtonO.OnInteract += delegate { LogState();  HandleButtonPress(o: true); return false; };
        ButtonI.OnInteract += delegate { LogState();  HandleButtonPress(o: false); return false; };
        Bulb.OnInteract += delegate { LogState();  HandleBulb(); return false; };

        Module.OnActivate += delegate
        {
            TurnLights(on: _initiallyOn);
            _stage = 1;
        };
    }

    private void LogState()
    {
        Debug.LogFormat("Current state: wentOffAtStep1={0}, rememberedIndicatorPresent={1}, isBulbUnscrewed={2}, mustUndoBulbScrewing={3}, pressedOAtStep1={4}, wentOnAtScrewIn={5}, stage={6}",
            _wentOffAtStep1, _rememberedIndicatorPresent, _isBulbUnscrewed, _mustUndoBulbScrewing, _pressedOAtStep1, _wentOnAtScrewIn, _stage);
    }

    private void TurnLights(bool on)
    {
        Light1.enabled = on;
        Light2.enabled = on;
    }

    private void HandleBulb()
    {
        // TODO: Animate screwing

        _isBulbUnscrewed = !_isBulbUnscrewed;

        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[TheLamp] Undoing incorrect {0}.", _isBulbUnscrewed ? "screwing in" : "unscrewing");
            _mustUndoBulbScrewing = false;
            return;
        }

        var isCorrect = true;
        var origStage = _stage;

        if (_stage >= 200)
            _stage -= 200;
        else if (_stage >= 100)
        {
            _stage -= 100;
            if (new[] { 7, 8, 9, 10 }.Contains(_stage))
                TurnLights(on: _wentOnAtScrewIn = (Rnd.Range(0, 2) == 0));
        }
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
                    if (isCorrect = (_lampColor != LampColor.Red && _lampColor != LampColor.White))
                        _stage = 7;
                    break;

                case 3:
                    if (isCorrect = (_lampColor != LampColor.Green && _lampColor != LampColor.Purple))
                        _stage = 8;
                    break;

                case 9:
                    isCorrect = (_lampColor == LampColor.Purple || _lampColor == LampColor.Red) && !_isBulbUnscrewed;
                    break;

                case 10:
                    isCorrect = (_lampColor == LampColor.Green || _lampColor == LampColor.White) && !_isBulbUnscrewed;
                    break;
            }
        }

        Debug.LogFormat("[TheLamp] {0} at stage {1}: {2}.", _isBulbUnscrewed ? "Unscrewing" : "Screwing in", origStage, isCorrect ? "CORRECT, stage is now: " + _stage : "WRONG");
        if (isCorrect && _stage == 0)
            Module.HandlePass();
        else if (!isCorrect)
        {
            Module.HandleStrike();
            _mustUndoBulbScrewing = true;
        }
    }

    private void HandleButtonPress(bool o)
    {
        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[TheLamp] The light bulb should have been {0} before pressing any more buttons.", _isBulbUnscrewed ? "screwed back in" : "unscrewed again");
            Module.HandleStrike();
            return;
        }

        var isCorrect = false;
        var origStage = _stage;

        switch (_stage)
        {
            case 1:
                if (_initiallyOn && (_opaque ? o : !o))
                {
                    isCorrect = true;
                    if (_opaque
                        ? (_lampColor == LampColor.Green || _lampColor == LampColor.Purple)
                        : (_lampColor == LampColor.Red || _lampColor == LampColor.White))
                        TurnLights(on: _wentOffAtStep1 = (Rnd.Range(0, 2) == 0));
                    else
                        TurnLights(on: false);
                    _stage = o ? 3 : 2;
                    _pressedOAtStep1 = o;
                }
                break;

            case 2:
                if ((_lampColor == LampColor.Red && !o) || (_lampColor == LampColor.White && o))
                {
                    isCorrect = true;
                    TurnLights(on: false);
                    _stage = o ? 106 : 105;
                }
                break;

            case 3:
                if ((_lampColor == LampColor.Green && !o) || (_lampColor == LampColor.Purple && o))
                {
                    isCorrect = true;
                    TurnLights(on: false);
                    _stage = o ? 105 : 106;
                }
                break;

            case 4:
                if (isCorrect = (Bomb.IsIndicatorOn(KMBombInfoExtensions.KnownIndicatorLabel.CAR) ||
                    Bomb.IsIndicatorOn(KMBombInfoExtensions.KnownIndicatorLabel.IND) ||
                    Bomb.IsIndicatorOn(KMBombInfoExtensions.KnownIndicatorLabel.MSA) ||
                    Bomb.IsIndicatorOn(KMBombInfoExtensions.KnownIndicatorLabel.SND) ? !o : o))
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
                _rememberedIndicatorPresent = Bomb.IsIndicatorPresent(_lampColor == LampColor.Blue ? KMBombInfoExtensions.KnownIndicatorLabel.CLR : KMBombInfoExtensions.KnownIndicatorLabel.SIG);
                if (isCorrect = ((_lampColor == LampColor.Green) || (_lampColor == LampColor.Purple) ? !o : o))
                    _stage = (_lampColor == LampColor.Blue) || (_lampColor == LampColor.Green) ? 11 : (_lampColor == LampColor.Purple) ? 212 : 213;
                break;

            case 8:
                _rememberedIndicatorPresent = Bomb.IsIndicatorPresent(_lampColor == LampColor.White ? KMBombInfoExtensions.KnownIndicatorLabel.FRQ : KMBombInfoExtensions.KnownIndicatorLabel.FRK);
                if (isCorrect = ((_lampColor == LampColor.White) || (_lampColor == LampColor.Red) ? !o : o))
                    _stage = (_lampColor == LampColor.White) || (_lampColor == LampColor.Yellow) ? 11 : (_lampColor == LampColor.Red) ? 213 : 212;
                break;

            case 9:
                if (isCorrect = (
                    _lampColor == LampColor.Blue || _lampColor == LampColor.Green ? !o :
                    _lampColor == LampColor.Yellow || _lampColor == LampColor.White ? o :
                    _lampColor == LampColor.Purple ? !_isBulbUnscrewed && !o : !_isBulbUnscrewed && o))
                    _stage =
                        _lampColor == LampColor.Blue ? 14 :
                        _lampColor == LampColor.Green ? 212 :
                        _lampColor == LampColor.Yellow ? 15 :
                        _lampColor == LampColor.White ? 213 :
                        _lampColor == LampColor.Purple ? 12 : 13;
                break;

            case 10:
                if (isCorrect = (
                    _lampColor == LampColor.Purple || _lampColor == LampColor.Red ? !o :
                    _lampColor == LampColor.Blue || _lampColor == LampColor.Yellow ? o :
                    _lampColor == LampColor.Green ? !_isBulbUnscrewed && !o : !_isBulbUnscrewed && o))
                    _stage =
                        _lampColor == LampColor.Purple ? 14 :
                        _lampColor == LampColor.Red ? 213 :
                        _lampColor == LampColor.Blue ? 15 :
                        _lampColor == LampColor.Yellow ? 212 :
                        _lampColor == LampColor.Green ? 13 : 12;
                break;

            case 11:
                if (isCorrect = (_rememberedIndicatorPresent ? !o : o))
                    _stage = 200;
                break;

            case 12:
                if (isCorrect = (_wentOnAtScrewIn ? !o : o))
                    _stage = 0;
                break;

            case 13:
                if (isCorrect = (_wentOnAtScrewIn ? o : !o))
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

        Debug.LogFormat("[TheLamp] Pressing {0} at stage {1} with the bulb {2}: {3}.", o ? "O" : "I", origStage, _isBulbUnscrewed ? "unscrewed" : "screwed in", isCorrect ? "CORRECT, stage is now: " + _stage : "WRONG");
        if (isCorrect && _stage == 0)
            Module.HandlePass();
        else if (!isCorrect)
            Module.HandleStrike();
    }
}
