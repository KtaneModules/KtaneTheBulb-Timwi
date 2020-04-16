using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KModkit;
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
    public KMColorblindMode ColorblindMode;
    public KMRuleSeedable RuleSeedable;

    public Light Light1;
    public Light Light2;
    public MeshRenderer Glass;
    public GameObject Filament;
    public TextMesh ColorBlindIndicator;

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

    private static readonly BulbColor[] _colors = new[] { BulbColor.Blue, BulbColor.Red, BulbColor.Green, BulbColor.Yellow, BulbColor.White, BulbColor.Purple };

    private static readonly Color[] _haloColors = Ext.NewArray(
        "6AA8FF",   // blue
        "FF0005",   // red
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    private static readonly Color[] _bulbColors = Ext.NewArray(
        "1B3E70",   // blue
        "FF0005",   // red
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    private static readonly List<object> _conditions;

    private BulbColor _bulbColor;
    private bool _opaque;
    private bool _initiallyOn;
    private bool _wentOffAtStep1;
    private bool _rememberedRule;
    private bool _isBulbUnscrewed;
    private bool _mustUndoBulbScrewing;
    private bool _pressedOAtStep1;
    private bool _goOnAtScrewIn;
    private bool _wasOnAtUnscrew;
    private int _stage;
    private string _correctButtonPresses;
    private bool _isSolved;
    private bool _isButtonDown;
    private bool _isLongPress;
    private bool _isIOnLeft;
    private bool _isOn;
    private Coroutine _buttonDownCoroutine;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    // For rule seed
    struct StepRule
    {
        // Param: whether “O” was pressed (otherwise it was “I”)
        // Return: null if strike; otherwise new step number, potentially plus 100 (unscrew first)/200 (screw first); 0 for solve
        public Func<bool, int?> ButtonPress;
        public Func<int?> BulbScrew;
    }
    private StepRule[] _rules;

    static TheBulbModule()
    {
        _conditions = new List<object>();
        _conditions.Add(new Func<TheBulbModule, bool>(b => !b._opaque));
        _conditions.Add(new Func<TheBulbModule, bool>(b => b._isIOnLeft));

        _conditions.Add(Ext.NewArray(
            Ext.NewArray<Func<KMBombInfo, bool>>(
                // the last digit of the serial number is even
                b => b.GetSerialNumberNumbers().Last() % 2 == 0,
                // the third character of the serial number is an even digit
                b => (b.GetSerialNumber()[2] - '0') % 2 == 0,
                // the first character of the serial number is a letter
                b => b.GetSerialNumber()[0] >= 'A' && b.GetSerialNumber()[0] <= 'Z',
                // the second character of the serial number is a letter
                b => b.GetSerialNumber()[1] >= 'A' && b.GetSerialNumber()[1] <= 'Z',
                // the serial number contains a vowel
                b => b.GetSerialNumber().Any(ch => "AEIOU".Contains(ch)),
                // the serial number contains an even digit
                b => b.GetSerialNumber().Any(ch => "02468".Contains(ch)),
                // the serial number contains a duplicated character
                b => { var sn = b.GetSerialNumber(); for (var i = 0; i < sn.Length; i++) for (var j = i + 1; j < sn.Length; j++) if (sn[i] == sn[j]) return true; return false; },
                // the serial number contains three letters and three digits
                b => b.GetSerialNumberLetters().Count() == 3),

            Ext.NewArray<Func<KMBombInfo, bool>>(
                // the bomb has a parallel port
                b => b.IsPortPresent(Port.Parallel),
                // the bomb has a serial port
                b => b.IsPortPresent(Port.Serial),
                // the bomb has a PS/2 port
                b => b.IsPortPresent(Port.PS2),
                // the bomb has a Stereo RCA port
                b => b.IsPortPresent(Port.StereoRCA),
                // the bomb has a RJ-45 port
                b => b.IsPortPresent(Port.RJ45),
                // the bomb has a DVI-D port
                b => b.IsPortPresent(Port.DVI),
                // the bomb has a duplicate port
                b => b.IsDuplicatePortPresent(),
                // the bomb has an empty port plate
                b => b.GetPortPlates().Any(p => p.Length == 0),
                // the bomb has an even number of ports
                b => b.GetPortCount() % 2 == 0,
                // the bomb has an odd number of ports
                b => b.GetPortCount() % 2 == 1,
                // the bomb has an even number of port plates
                b => b.GetPortPlateCount() % 2 == 0,
                // the bomb has an odd number of port plates
                b => b.GetPortPlateCount() % 2 == 1,
                // the bomb has an even number of unique port types
                b => b.CountUniquePorts() % 2 == 0,
                // the bomb has an odd number of unique port types
                b => b.CountUniquePorts() % 2 == 1),

            Ext.NewArray<Func<KMBombInfo, bool>>(
                // the bomb has a lit indicator
                b => b.GetOnIndicators().Any(),
                // the bomb has an unlit indicator
                b => b.GetOffIndicators().Any(),
                // the bomb has an indicator with a vowel
                b => b.GetIndicators().Any(ind => ind.Any(ch => "AEIOU".Contains(ch))),
                // the bomb has an even number of indicators
                b => b.GetIndicators().Count() % 2 == 0,
                // the bomb has an odd number of indicators
                b => b.GetIndicators().Count() % 2 == 1,
                // the bomb has an even number of lit indicators
                b => b.GetOnIndicators().Count() % 2 == 0,
                // the bomb has an odd number of lit indicators
                b => b.GetOnIndicators().Count() % 2 == 1,
                // the bomb has an even number of unlit indicators
                b => b.GetOffIndicators().Count() % 2 == 0,
                // the bomb has an odd number of unlit indicators
                b => b.GetOffIndicators().Count() % 2 == 1,
                // the bomb has a BOB indicator
                b => b.IsIndicatorPresent(Indicator.BOB),
                // the bomb has a CAR indicator
                b => b.IsIndicatorPresent(Indicator.CAR),
                // the bomb has a CLR indicator
                b => b.IsIndicatorPresent(Indicator.CLR),
                // the bomb has an FRK indicator
                b => b.IsIndicatorPresent(Indicator.FRK),
                // the bomb has an FRQ indicator
                b => b.IsIndicatorPresent(Indicator.FRQ),
                // the bomb has an IND indicator
                b => b.IsIndicatorPresent(Indicator.IND),
                // the bomb has an MSA indicator
                b => b.IsIndicatorPresent(Indicator.MSA),
                // the bomb has an NSA indicator
                b => b.IsIndicatorPresent(Indicator.NSA),
                // the bomb has an SIG indicator
                b => b.IsIndicatorPresent(Indicator.SIG),
                // the bomb has an SND indicator
                b => b.IsIndicatorPresent(Indicator.SND),
                // the bomb has a TRN indicator
                b => b.IsIndicatorPresent(Indicator.TRN)),

            Ext.NewArray<Func<KMBombInfo, bool>>(
                // the bomb has any AA batteries
                b => b.GetBatteryCount(Battery.AA) + b.GetBatteryCount(Battery.AAx3) + b.GetBatteryCount(Battery.AAx4) > 0,
                // the bomb has any D batteries
                b => b.GetBatteryCount(Battery.D) > 0,
                // the bomb has an even number of batteries
                b => b.GetBatteryCount() % 2 == 0,
                // the bomb has an odd number of batteries
                b => b.GetBatteryCount() % 2 == 1,
                // the bomb has an even number of battery holders
                b => b.GetBatteryHolderCount() % 2 == 0,
                // the bomb has an odd number of battery holders
                b => b.GetBatteryHolderCount() % 2 == 1)));
    }

    static object deepCopy(object orig)
    {
        return orig is IList ? ((IList) orig).Cast<object>().Select(deepCopy).ToList() : orig;
    }

    enum ExtraRandom
    {
        IsLightOnNow,
        WentOffAtStep1
    }

    private static readonly int? strike = null;
    private static readonly ExtraRandom? none = null;

    static object getRandom(MonoRandom rnd, IList arr, ExtraRandom? extra = null)
    {
        if (extra != null && rnd.Next(0, 2) != 0)
            return extra.Value;

        var ix = rnd.Next(0, arr.Count);
        var elem = arr[ix];
        var list = elem as IList;
        if (list != null)
        {
            var res = getRandom(rnd, list);
            if (list.Count == 0)
                arr.RemoveAt(ix);
            return res;
        }
        arr.RemoveAt(ix);
        return elem;
    }

    bool eval(object rule)
    {
        if (rule is Func<KMBombInfo, bool>)
            return ((Func<KMBombInfo, bool>) rule)(Bomb);
        else if (rule is Func<TheBulbModule, bool>)
            return ((Func<TheBulbModule, bool>) rule)(this);
        else if (rule is ExtraRandom)
            return rule.Equals(ExtraRandom.IsLightOnNow) ? (_stage == 12 || _stage == 13 ? _goOnAtScrewIn : _isOn) : _wentOffAtStep1;
        else
        {
            Debug.LogFormat(@"<The Bulb #{0}> eval() encountered unexpected “{1}”", _moduleId, rule);
            return false;
        }
    }

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _isSolved = false;

        _isIOnLeft = Rnd.Range(0, 2) == 0;
        if (_isIOnLeft)
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
        _opaque = Rnd.Range(0, 2) == 0;
        _initiallyOn = Rnd.Range(0, 2) == 0;

        _bulbColor = (BulbColor) colorIndex;
        Filament.SetActive(!_opaque);
        Glass.material.color = _bulbColors[colorIndex].WithAlpha(_opaque ? 1f : .55f);
        Light1.color = Light2.color = _haloColors[colorIndex].WithAlpha(_opaque ? 1f : .55f);
        _stage = -1;
        _isBulbUnscrewed = false;
        _correctButtonPresses = "";

        Debug.LogFormat("[The Bulb #{3}] Bulb is {0}, {1} and {2}. I is on the {4}.", _bulbColor, _opaque ? "opaque" : "see-through", _initiallyOn ? "on" : "off", _moduleId, _isIOnLeft ? "left" : "right");

        ButtonO.OnInteract += delegate { ButtonO.AddInteractionPunch(); _buttonDownCoroutine = StartCoroutine(HandleLongPress(o: true)); return false; };
        ButtonI.OnInteract += delegate { ButtonI.AddInteractionPunch(); _buttonDownCoroutine = StartCoroutine(HandleLongPress(o: false)); return false; };
        ButtonO.OnInteractEnded += delegate { HandleButtonUp(o: true); };
        ButtonI.OnInteractEnded += delegate { HandleButtonUp(o: false); };
        Bulb.OnInteract += delegate { HandleBulb(); return false; };

        ColorBlindIndicator.text = _bulbColor.ToString();
        ColorBlindIndicator.gameObject.SetActive(ColorblindMode.ColorblindModeActive);

        TurnLights(on: _initiallyOn);
        _stage = 1;

        float scalar = transform.lossyScale.x;
        Light1.range *= scalar;
        Light2.range *= scalar;


        // RULE SEED
        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat(@"[The Bulb #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);
        if (rnd.Seed == 1)
            _rules = null;
        else
        {
            var conditions = (IList) deepCopy(_conditions);

            // we’re numbering the steps from 1, so index [0] will remain unused
            _rules = new StepRule[16];
            var stepsTypes = new int[16];
            var stepsConds = new object[16];

            var colors1 = rnd.ShuffleFisherYates(_colors.ToArray());
            Debug.LogFormat(@"<The Bulb #{0}> Step 1 colors: {1}", _moduleId, string.Join(", ", colors1.Select(c => c.ToString()).ToArray()));
            var colors2_3 = new[] { rnd.ShuffleFisherYates(_colors.ToArray()), rnd.ShuffleFisherYates(_colors.ToArray()) };


            // *** START LEFT HALF (steps 1-3, 5-8)

            stepsTypes[1] = rnd.Next(0, 2);
            if (stepsTypes[1] == 0)
            {
                // Boolean condition
                stepsConds[1] = getRandom(rnd, conditions);
                _rules[1] = new StepRule
                {
                    ButtonPress = o => !_initiallyOn ? strike : eval(stepsConds[1]) ? (!o ? 2 : strike) : (o ? 3 : strike),
                    BulbScrew = () => _initiallyOn ? strike : 4
                };
            }
            else
            {
                // Colors
                _rules[1] = new StepRule
                {
                    ButtonPress = o => !_initiallyOn ? strike : colors1.Take(3).Contains(_bulbColor) ? (!o ? 2 : strike) : (o ? 3 : strike),
                    BulbScrew = () => _initiallyOn ? strike : 4
                };
            }

            // Take a copy of the remaining conditions so that the right half can re-use them
            var rightConditions = (IList) deepCopy(conditions);

            for (var step = 2; step <= 3; step++)
            {
                var isStep2 = step == 2;
                stepsTypes[step] = stepsTypes[1] == 1 ? 2 : rnd.Next(0, 2);
                var col = colors2_3[step - 2];
                Debug.LogFormat(@"<The Bulb #{0}> Step {1} colors: {2}", _moduleId, step, string.Join(", ", col.Select(c => c.ToString()).ToArray()));
                if (stepsTypes[step] == 0)
                {
                    // All colors
                    _rules[step] = new StepRule
                    {
                        ButtonPress = o => _bulbColor == col[0] ? (!o ? (isStep2 ? 105 : 106) : strike) : _bulbColor == col[1] ? (o ? (isStep2 ? 106 : 105) : strike) : strike,
                        BulbScrew = () => _bulbColor == col[0] || _bulbColor == col[1] ? strike : (isStep2 ? 7 : 8)
                    };
                }
                else if (stepsTypes[step] == 1)
                {
                    // 4 colors + a boolean
                    var thisCond = stepsConds[step] = getRandom(rnd, conditions, ExtraRandom.IsLightOnNow);
                    _rules[step] = new StepRule
                    {
                        ButtonPress = o => col.Skip(2).Contains(_bulbColor) ? strike : eval(thisCond) ? (!o ? (isStep2 ? 106 : 105) : strike) : (o ? (isStep2 ? 105 : 106) : strike),
                        BulbScrew = () => col.Skip(2).Contains(_bulbColor) ? (isStep2 ? 7 : 8) : strike
                    };
                }
                else if (stepsTypes[step] == 2)
                {
                    // 2 colors + a boolean
                    var thisCond = stepsConds[step] = getRandom(rnd, conditions, ExtraRandom.IsLightOnNow);
                    _rules[step] = new StepRule
                    {
                        ButtonPress = o => _bulbColor == colors1[isStep2 ? 1 : 4] || _bulbColor == colors1[isStep2 ? 2 : 5] ? strike : eval(thisCond) ? (!o ? (isStep2 ? 106 : 105) : strike) : (o ? (isStep2 ? 105 : 106) : strike),
                        BulbScrew = () => _bulbColor == colors1[isStep2 ? 1 : 4] || _bulbColor == colors1[isStep2 ? 2 : 5] ? (isStep2 ? 7 : 8) : strike
                    };
                }
            }

            for (var step = 5; step <= 6; step++)
            {
                var isStep5 = step == 5;
                object cond;
                // Check if steps 5 & 6 can use the bulb color, and if so, use it with 90% likelihood
                if (stepsTypes[2] == 1 && stepsTypes[3] == 1 && rnd.Next(0, 10) != 0)
                {
                    var c21 = colors2_3[0][0];
                    var c22 = colors2_3[0][1];
                    var c31 = colors2_3[1][0];
                    var c32 = colors2_3[1][1];

                    if (c21 == c32 || c22 == c31)
                    {
                        var t = c31;
                        c31 = c32;
                        c32 = t;
                    }

                    cond = isStep5
                        ? new Func<TheBulbModule, bool>(b => _bulbColor == c21 || _bulbColor == c31)
                        : new Func<TheBulbModule, bool>(b => _bulbColor == c22 || _bulbColor == c32);
                }
                else
                {
                    stepsConds[step] = getRandom(rnd, conditions, (object.Equals(stepsConds[2], ExtraRandom.IsLightOnNow) || object.Equals(stepsConds[3], ExtraRandom.IsLightOnNow)) ? none : ExtraRandom.WentOffAtStep1);
                    cond = stepsConds[step];
                }

                _rules[step] = new StepRule
                {
                    ButtonPress = o => eval(cond) ? (!o ? 200 : strike) : (o ? 200 : strike),
                    BulbScrew = () => strike
                };
            }

            for (var step = 7; step <= 8; step++)
            {
                var cond1 = getRandom(rnd, conditions);
                var cond2 = getRandom(rnd, conditions);
                var col = colors2_3[step - 7];
                Debug.LogFormat(@"<The Bulb #{0}> Step {1} colors: {2}", _moduleId, step, string.Join(", ", col.Select(c => c.ToString()).ToArray()));
                var isStep7 = step == 7;
                if (stepsTypes[step - 5] != 2)
                {
                    // Must use up all four of the remaining colors
                    stepsTypes[step] = 2;
                    _rules[step] = new StepRule
                    {
                        ButtonPress = o =>
                        {
                            if (_bulbColor == col[2])
                            {
                                _rememberedRule = eval(cond1);
                                return !o ? 11 : strike;
                            }
                            else if (_bulbColor == col[3])
                                return !o ? 212 : strike;
                            else if (_bulbColor == col[4])
                            {
                                _rememberedRule = eval(cond2);
                                return o ? 11 : strike;
                            }
                            return o ? 213 : strike;
                        },
                        BulbScrew = () => strike
                    };
                }
                else
                {
                    stepsTypes[step] = rnd.Next(0, 2);
                    var b2 = getRandom(rnd, conditions);
                    var b1 = stepsTypes[step] == 0
                        // Two colors and a boolean
                        ? new Func<TheBulbModule, bool>(b => b._bulbColor == colors1[isStep7 ? 1 : 4])
                        // Two booleans
                        : getRandom(rnd, conditions);

                    _rules[step] = new StepRule
                    {
                        ButtonPress = o =>
                        {
                            var v = eval(b1);
                            _rememberedRule = eval(v ? cond1 : cond2);
                            return v
                                ? (o ? strike : eval(b2) ? 11 : 212)
                                : (!o ? strike : eval(b2) ? 11 : 213);
                        },
                        BulbScrew = () => strike
                    };
                }
            }

            // Step 11 is always the same; it relies on a condition “remembered” by a previous step
            _rules[11] = new StepRule
            {
                ButtonPress = o => _rememberedRule != o ? 200 : strike,
                BulbScrew = () => strike
            };

            // *** START RIGHT HALF (steps 4, 9-10)
            stepsTypes[4] = rnd.Next(0, 2);
            if (stepsTypes[4] == 0)
            {
                // Step 4 is a boolean, steps 9 and 10 use the colors
                var colors9_10 = new[] { rnd.ShuffleFisherYates(_colors.ToArray()), rnd.ShuffleFisherYates(_colors.ToArray()) };
                stepsConds[4] = getRandom(rnd, rightConditions);
                _rules[4] = new StepRule
                {
                    ButtonPress = o => eval(stepsConds[4]) ? (!o ? 9 : strike) : (o ? 10 : strike),
                    BulbScrew = () => strike
                };

                for (var step = 9; step <= 10; step++)
                {
                    var col = colors9_10[step - 9];
                    Debug.LogFormat(@"<The Bulb #{0}> Step {1} colors: {2}", _moduleId, step, string.Join(", ", col.Select(c => c.ToString()).ToArray()));
                    _rules[step] = new StepRule
                    {
                        ButtonPress = o =>
                            _bulbColor == col[0] ? (!o ? 14 : strike) :
                            _bulbColor == col[1] ? (!o ? 212 : strike) :
                            _bulbColor == col[2] ? (o ? 15 : strike) :
                            _bulbColor == col[3] ? (o ? 213 : strike) :
                            _bulbColor == col[4]
                                ? (_isBulbUnscrewed ? strike : (!o ? 12 : strike))
                                : (_isBulbUnscrewed ? strike : (o ? 13 : strike)),

                        // Note that _isBulbUnscrewed here has the NEW value because this is evaluated AFTER the boolean is flipped
                        BulbScrew = () => _isBulbUnscrewed ? strike : 9
                    };
                }
            }
            else
            {
                // Step 4 uses colors and 9/10 are colors combined with a boolean
                var colors4 = rnd.ShuffleFisherYates(_colors.ToArray());
                Debug.LogFormat(@"<The Bulb #{0}> Step {1} colors: {2}", _moduleId, 4, string.Join(", ", colors4.Select(c => c.ToString()).ToArray()));
                _rules[4] = new StepRule
                {
                    ButtonPress = o => colors4.Take(3).Contains(_bulbColor) ? (!o ? 9 : strike) : (o ? 10 : strike),
                    BulbScrew = () => strike
                };

                for (var step = 9; step <= 10; step++)
                {
                    var cond = stepsConds[step] = getRandom(rnd, rightConditions);
                    var isStep9 = step == 9;
                    _rules[step] = new StepRule
                    {
                        ButtonPress = o =>
                        {
                            if (eval(cond))
                            {
                                if (_bulbColor == colors4[isStep9 ? 0 : 3])
                                    return !o ? 14 : strike;
                                else if (_bulbColor == colors4[isStep9 ? 1 : 4])
                                    return !o ? (isStep9 ? 212 : 213) : strike;
                                return o ? 15 : strike;
                            }
                            else
                            {
                                if (_bulbColor == colors4[isStep9 ? 0 : 3])
                                    return o ? (isStep9 ? 213 : 212) : strike;
                                else if (_bulbColor == colors4[isStep9 ? 1 : 4])
                                    return !_isBulbUnscrewed && !o ? (isStep9 ? 12 : 13) : strike;
                                return !_isBulbUnscrewed && o ? (isStep9 ? 13 : 12) : strike;
                            }
                        },
                        // Note that _isBulbUnscrewed here has the NEW value because this is evaluated AFTER the boolean is flipped
                        BulbScrew = () => eval(cond) || _bulbColor == colors4[isStep9 ? 0 : 3] || _isBulbUnscrewed ? strike : isStep9 ? 9 : 10
                    };
                }
            }

            stepsConds[14] = getRandom(rnd, rightConditions);
            _rules[14] = new StepRule { ButtonPress = o => eval(stepsConds[14]) ? (!o ? 200 : strike) : (o ? 200 : strike), BulbScrew = () => strike };
            _rules[15] = new StepRule { ButtonPress = o => eval(stepsConds[14]) ? (o ? 200 : strike) : (!o ? 200 : strike), BulbScrew = () => strike };

            // Steps 12 and 13 are the only ones shared by both left and right halves.
            // Not a big deal if this one rarely re-uses a condition already used by the right half.
            // Also, they can safely use the current lit state regardless of earlier stages.
            stepsConds[12] = getRandom(rnd, conditions, ExtraRandom.IsLightOnNow);
            _rules[12] = new StepRule { ButtonPress = o => eval(stepsConds[12]) ? (!o ? 0 : strike) : (o ? 0 : strike), BulbScrew = () => strike };
            _rules[13] = new StepRule { ButtonPress = o => eval(stepsConds[12]) ? (o ? 0 : strike) : (!o ? 0 : strike), BulbScrew = () => strike };
        }
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
        _isOn = on;
    }

    private bool _isScrewing;

    private IEnumerator Screw(bool @in)
    {
        if (!@in)
        {
            _wasOnAtUnscrew = Light1.enabled;
            TurnLights(on: false);
        }

        Audio.PlaySoundAtTransform(@in ? "ScrewIn" : "Unscrew", Bulb.transform);

        const float duration = 1f;
        var elapsed = 0f;
        var totalAngle = 540;
        var distance = 1.5f;
        while (elapsed < duration)
        {
            Bulb.transform.localEulerAngles = new Vector3(0, totalAngle * ((@in ? duration - elapsed : elapsed) / duration), 0);
            Bulb.transform.localPosition = new Vector3(0, distance * (@in ? duration - elapsed : elapsed) / duration, 0);
            yield return null;
            elapsed += Time.deltaTime;
        }
        Bulb.transform.localEulerAngles = new Vector3(0, @in ? 0 : totalAngle, 0);
        Bulb.transform.localPosition = new Vector3(0, @in ? 0 : distance, 0);
        _isScrewing = false;

        if (_isSolved)
            yield break;

        if (@in && (_wasOnAtUnscrew || _goOnAtScrewIn))
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
                _goOnAtScrewIn = (Rnd.Range(0, 2) == 0);
                extra = string.Format(" Light {0} at screw-in.", _goOnAtScrewIn ? "came on" : "stayed off");
            }
        }
        else if (_stage >= 100)
            _stage -= 100;
        else
        {
            isCorrect = false;

            if (_rules == null)
            {
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
            else
            {
                var result = _rules[_stage].BulbScrew();
                if (isCorrect = (result != null))
                    _stage = result.Value;
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
        _isLongPress = false;
        Audio.PlaySoundAtTransform("ButtonClick", (o ? ButtonO : ButtonI).transform);
        yield return new WaitForSeconds(.7f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, Bulb.transform);
        _isLongPress = true;
    }

    private void HandleButtonUp(bool o)
    {
        if (!_isButtonDown || _isSolved)
            return;

        if (_buttonDownCoroutine != null)
        {
            StopCoroutine(_buttonDownCoroutine);
            _buttonDownCoroutine = null;
        }
        _isButtonDown = false;

        if (_isLongPress)
        {
            _isLongPress = false;

            if (_isBulbUnscrewed)
            {
                Debug.LogFormat("[The Bulb #{0}] You tried to reset while the bulb was unscrewed. That is not allowed.", _moduleId);
                Module.HandleStrike();
                return;
            }

            TurnLights(on: _initiallyOn);
            _stage = 1;
            _mustUndoBulbScrewing = false;
            _correctButtonPresses = "";
            Debug.LogFormat("[The Bulb #{0}] Module reset.", _moduleId);
            return;
        }

        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[The Bulb #{1}] The light bulb should have been {0} before pressing any more buttons.", _isBulbUnscrewed ? "screwed back in" : "unscrewed again", _moduleId);
            Module.HandleStrike();
            return;
        }

        var isCorrect = false;
        var extra = "";

        if (_rules == null)
        {
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
                            extra = _wentOffAtStep1 ? " Light went off." : " Light stayed on.";
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
                    if (isCorrect = (Bomb.IsIndicatorPresent(Indicator.CAR) ||
                        Bomb.IsIndicatorPresent(Indicator.IND) ||
                        Bomb.IsIndicatorPresent(Indicator.MSA) ||
                        Bomb.IsIndicatorPresent(Indicator.SND) ? !o : o))
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
                    _rememberedRule = Bomb.IsIndicatorPresent(_bulbColor == BulbColor.Blue ? Indicator.CLR : Indicator.SIG);
                    if (isCorrect = ((_bulbColor == BulbColor.Green) || (_bulbColor == BulbColor.Purple) ? !o : o))
                        _stage = (_bulbColor == BulbColor.Blue) || (_bulbColor == BulbColor.Green) ? 11 : (_bulbColor == BulbColor.Purple) ? 212 : 213;
                    break;

                case 8:
                    _rememberedRule = Bomb.IsIndicatorPresent(_bulbColor == BulbColor.White ? Indicator.FRQ : Indicator.FRK);
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
                            var randomState = Rnd.Range(0, 2) == 0;
                            extra = string.Format(" Light {0} at {1} button press.", randomState ? "came on" : "stayed off", o ? "O" : "I");
                            TurnLights(on: randomState);
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
                            var randomState = Rnd.Range(0, 2) == 0;
                            extra = string.Format(" Light {0} at {1} button press.", randomState ? "came on" : "stayed off", o ? "O" : "I");
                            TurnLights(on: randomState);
                        }
                    }
                    break;

                case 11:
                    if (isCorrect = (_rememberedRule ? !o : o))
                        _stage = 200;
                    break;

                case 12:
                    if (isCorrect = (_isOn ? !o : o))
                        _stage = 0;
                    break;

                case 13:
                    if (isCorrect = (_isOn ? o : !o))
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
        }
        else
        {
            var result = _stage >= 1 && _stage <= 15 ? _rules[_stage].ButtonPress(o) : null;
            if (isCorrect = (result != null))
            {
                _stage = result.Value;

                // Step 2/3: if there’s another button to press, decide by random whether this button press turns the light off or not
                if ((_stage == 2 || _stage == 3) && _rules[_stage].BulbScrew() == null)
                {
                    TurnLights(on: !(_wentOffAtStep1 = (Rnd.Range(0, 2) == 0)));
                    extra = _wentOffAtStep1 ? " Light went off." : " Light stayed on.";
                }
                // Step 2/3: if the bulb must be unscrewed next, turn the light off
                else if (_stage == 2 || _stage == 3)
                    TurnLights(on: false);
                // Step 12/13: bulb is in, one more button to press. Decide at random whether to turn the light on or not
                else if (_stage == 12 || _stage == 13)
                {
                    TurnLights(on: _goOnAtScrewIn = (Rnd.Range(0, 2) == 0));
                    extra = string.Format(" Light {0} at {1} button press.", _goOnAtScrewIn ? "came on" : "stayed off", o ? "O" : "I");
                }
                else if (_stage % 100 == 5 || _stage % 100 == 6)
                    TurnLights(on: false);
            }
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
    private readonly string TwitchHelpMessage = @"!{0} O | !{0} I | !{0} screw | !{0} unscrew | !{0} O, unscrew, I, screw | !{0} reset | !{0} colorblind";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (command == "colorblind")
        {
            ColorBlindIndicator.gameObject.SetActive(true);
            yield return null;
            yield break;
        }

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

    IEnumerator TwitchHandleForcedSolve()
    {
        while (_stage > 0)
        {
            while (_isScrewing)
                yield return true;

            KMSelectable buttonToPress = null;

            if (_mustUndoBulbScrewing)
                buttonToPress = Bulb;
            else if (_rules == null)
            {
                switch (_stage)
                {
                    case 1:
                        buttonToPress = !_initiallyOn ? Bulb : _opaque ? ButtonO : ButtonI;
                        break;

                    case 2:
                        buttonToPress = _bulbColor == BulbColor.Red ? ButtonI : _bulbColor == BulbColor.White ? ButtonO : Bulb;
                        break;

                    case 3:
                        buttonToPress = _bulbColor == BulbColor.Green ? ButtonI : _bulbColor == BulbColor.Purple ? ButtonO : Bulb;
                        break;

                    case 4:
                        buttonToPress = Bomb.IsIndicatorPresent(Indicator.CAR) ||
                            Bomb.IsIndicatorPresent(Indicator.IND) ||
                            Bomb.IsIndicatorPresent(Indicator.MSA) ||
                            Bomb.IsIndicatorPresent(Indicator.SND) ? ButtonI : ButtonO;
                        break;

                    case 5:
                        buttonToPress = _wentOffAtStep1 ? (_pressedOAtStep1 ? ButtonO : ButtonI) : (_pressedOAtStep1 ? ButtonI : ButtonO);
                        break;

                    case 6:
                        buttonToPress = (_wentOffAtStep1 ^ _pressedOAtStep1) ? (_pressedOAtStep1 ? ButtonO : ButtonI) : (_pressedOAtStep1 ? ButtonI : ButtonO);
                        break;

                    case 7:
                        buttonToPress = (_bulbColor == BulbColor.Green) || (_bulbColor == BulbColor.Purple) ? ButtonI : ButtonO;
                        break;

                    case 8:
                        buttonToPress = (_bulbColor == BulbColor.White) || (_bulbColor == BulbColor.Red) ? ButtonI : ButtonO;
                        break;

                    case 9:
                        buttonToPress =
                            _bulbColor == BulbColor.Blue || _bulbColor == BulbColor.Green ? ButtonI :
                            _bulbColor == BulbColor.Yellow || _bulbColor == BulbColor.White ? ButtonO : Bulb;
                        break;

                    case 10:
                        buttonToPress =
                            _bulbColor == BulbColor.Purple || _bulbColor == BulbColor.Red ? ButtonI :
                            _bulbColor == BulbColor.Blue || _bulbColor == BulbColor.Yellow ? ButtonO : Bulb;
                        break;

                    case 11:
                        buttonToPress = _rememberedRule ? ButtonI : ButtonO;
                        break;

                    case 12:
                        buttonToPress = _isOn ? ButtonI : ButtonO;
                        break;

                    case 13:
                        buttonToPress = _isOn ? ButtonO : ButtonI;
                        break;

                    case 14:
                        buttonToPress = _opaque ? ButtonI : ButtonO;
                        break;

                    case 15:
                        buttonToPress = _opaque ? ButtonO : ButtonI;
                        break;

                    default:
                        buttonToPress = Bulb;
                        break;
                }
            }
            else
                buttonToPress = _stage >= 100 ? Bulb : _rules[_stage].BulbScrew() != null ? Bulb : _rules[_stage].ButtonPress(true) != null ? ButtonO : ButtonI;

            buttonToPress.OnInteract();
            yield return new WaitForSeconds(.1f);
            if (buttonToPress.OnInteractEnded != null)
            {
                buttonToPress.OnInteractEnded();
                yield return new WaitForSeconds(.1f);
            }
        }
    }
}
