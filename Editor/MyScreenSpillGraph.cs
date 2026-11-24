using System;
using Unity.VisualScripting;
using UnityEngine;

namespace MyScreen.Spill.Editor
{
    /// <summary>
    /// The spill hub's Visual Scripting graph. At Start and every N frames it reads the rig's markers and sets the
    /// spill's shader globals (MyScreenSpill.hlsl):
    ///   Screen, Screen1 ... Screen5 (MyScreenSpillScreen, MyScreenSpillScreen1 ... 5): position, right, up, -forward,
    ///     lossyScale -> each slot's centre, right/up + half sizes, normal (slot 0's globals have no suffix)
    ///   State  (MyScreenSpillState): localPosition x (2D), y (strength)          -> _MyScreenSpillFlat, x Intensity
    ///   Capture, Radius, Intensity, Mip (the hub's variables)                     -> the texture and the knobs
    /// Only members a Banter world graph may use: Shader.SetGlobal*, GameObject.transform, Transform's position, right,
    /// up, forward, lossyScale and localPosition, Vector3/Vector4 maths, Time.frameCount.
    /// </summary>
    public static class MyScreenSpillGraph
    {
        public static FlowGraph Build(int everyFrames)
        {
            var g = new FlowGraph { title = "MyScreen Spill Hub" };
            g.sticky.Add(new StickyNote
            {
                position = new Rect(-900, -260, 560, 150),
                title = "MyScreen Spill Hub",
                body = "Sets the spill's shader globals at Start and every " + everyFrames + " frames from MyScreenSpillScreen (where the light comes from: " +
                       "its front is -Z, its scale is the screen's width and height) and MyScreenSpillState (x: 1 = a 2D picture, y: strength). " +
                       "Tune Radius, Intensity and Mip on this object's Variables. MyScreen's gateway moves both markers at run time. Built by MyScreen > Spill > Add or Update Spill Rig: edits here are replaced the next time it runs.",
            });

            var start = Add(g, new Start { position = new Vector2(-900, -60) });
            var update = Add(g, new Update { position = new Vector2(-900, 40) });

            // The gate: every N frames.
            var frame = Get(g, typeof(Time), "frameCount", new Vector2(-900, 160));
            var mod = Add(g, new ScalarModulo { position = new Vector2(-700, 160) });
            Connect(g, frame.value, mod.dividend);
            mod.divisor.SetDefaultValue((float)everyFrames);
            var due = Add(g, new Equal { numeric = true, position = new Vector2(-500, 160) });
            Connect(g, mod.remainder, due.a);
            due.b.SetDefaultValue(0f);
            var gate = Add(g, new If { position = new Vector2(-700, 40) });
            g.controlConnections.Add(new ControlConnection(update.trigger, gate.enter));
            Connect(g, due.comparison, gate.condition);

            // The state marker: x = flat, y = strength.
            var state = Var(g, "State", new Vector2(-700, 760));
            var stateTf = Get(g, typeof(GameObject), "transform", new Vector2(-500, 760));
            Connect(g, state.value, stateTf.target);
            var local = Get(g, typeof(Transform), "localPosition", new Vector2(-300, 760));
            Connect(g, stateTf.value, local.target);
            var flat = Get(g, typeof(Vector3), "x", new Vector2(-100, 740));
            var gain = Get(g, typeof(Vector3), "y", new Vector2(-100, 800));
            Connect(g, local.value, flat.target);
            Connect(g, local.value, gain.target);
            var intensity = Var(g, "Intensity", new Vector2(-100, 880));
            var strength = Add(g, new ScalarMultiply { position = new Vector2(100, 860) });
            Connect(g, intensity.value, strength.a);
            Connect(g, gain.value, strength.b);

            var capture = Var(g, "Capture", new Vector2(600, -80));
            var radius = Var(g, "Radius", new Vector2(600, 0));
            var mip = Var(g, "Mip", new Vector2(600, 80));

            // Start and the gate both run the chain of sets.
            var setTex = new InvokeMember(new Member(typeof(Shader), "SetGlobalTexture", new[] { typeof(string), typeof(Texture) })) { position = new Vector2(900, -80) };
            Add(g, setTex);
            setTex.inputParameters[0].SetDefaultValue("_MyScreenSpillTex");
            Connect(g, capture.value, setTex.inputParameters[1]);
            g.controlConnections.Add(new ControlConnection(start.trigger, setTex.enter));
            g.controlConnections.Add(new ControlConnection(gate.ifTrue, setTex.enter));

            var last = setTex;
            last = SetFloat(g, last, "_MyScreenSpillRadius", radius.value, 1);
            last = SetFloat(g, last, "_MyScreenSpillIntensity", strength.product, 2);
            last = SetFloat(g, last, "_MyScreenSpillMip", mip.value, 3);
            last = SetFloat(g, last, "_MyScreenSpillFlat", flat.value, 4);
            // Each screen slot: its marker's place into its four globals.
            for (var slot = 0; slot < Slots; slot++)
            {
                var suffix = slot == 0 ? "" : slot.ToString();
                var s = ScreenSlot(g, "Screen" + suffix, new Vector2(0, 1100 + slot * 1000));
                var row = 5 + slot * 4;
                last = SetVector(g, last, "_MyScreenSpillCenter" + suffix, s.center, row);
                last = SetVector(g, last, "_MyScreenSpillRight" + suffix, s.rightW, row + 1);
                last = SetVector(g, last, "_MyScreenSpillUp" + suffix, s.upW, row + 2);
                last = SetVector(g, last, "_MyScreenSpillNormal" + suffix, s.normal, row + 3);
            }
            return g;
        }

        /// <summary>The screen slots (MYSCREEN_SPILL_SLOTS in MyScreenSpill.hlsl).</summary>
        public const int Slots = 6;

        /// <summary>A screen marker's four values: centre, right + half width, up + half height, normal.</summary>
        static (ValueOutput center, ValueOutput rightW, ValueOutput upW, ValueOutput normal) ScreenSlot(FlowGraph g, string variable, Vector2 at)
        {
            var screen = Var(g, variable, at + new Vector2(-700, 0));
            var tf = Get(g, typeof(GameObject), "transform", at + new Vector2(-500, 0));
            Connect(g, screen.value, tf.target);
            var pos = Get(g, typeof(Transform), "position", at + new Vector2(-300, -60));
            var right = Get(g, typeof(Transform), "right", at + new Vector2(-300, 0));
            var up = Get(g, typeof(Transform), "up", at + new Vector2(-300, 60));
            var fwd = Get(g, typeof(Transform), "forward", at + new Vector2(-300, 120));
            var scale = Get(g, typeof(Transform), "lossyScale", at + new Vector2(-300, 180));
            foreach (var m in new[] { pos, right, up, fwd, scale }) Connect(g, tf.value, m.target);

            var halfW = Half(g, scale, "x", at + new Vector2(-100, 180));
            var halfH = Half(g, scale, "y", at + new Vector2(-100, 280));
            var back = Add(g, new Vector3Multiply { position = at + new Vector2(-100, 120) });
            var minusOne = Add(g, new Literal(typeof(Vector3), new Vector3(-1f, -1f, -1f)) { position = at + new Vector2(-300, 240) });
            Connect(g, fwd.value, back.a);
            Connect(g, minusOne.output, back.b);

            return (
                Vec4(g, pos.value, null, at + new Vector2(200, -80)),
                Vec4(g, right.value, halfW, at + new Vector2(200, 100)),
                Vec4(g, up.value, halfH, at + new Vector2(200, 280)),
                Vec4(g, back.product, null, at + new Vector2(200, 460)));
        }

        static T Add<T>(FlowGraph g, T unit) where T : IUnit
        {
            g.units.Add(unit);
            return unit;
        }

        static void Connect(FlowGraph g, ValueOutput from, ValueInput to)
        {
            g.valueConnections.Add(new ValueConnection(from, to));
        }

        static GetVariable Var(FlowGraph g, string name, Vector2 at)
        {
            var v = Add(g, new GetVariable { kind = VariableKind.Object, position = at });
            v.name.SetDefaultValue(name);
            return v;
        }

        static GetMember Get(FlowGraph g, Type type, string member, Vector2 at)
        {
            return Add(g, new GetMember(new Member(type, member)) { position = at });
        }

        /// <summary>Half the absolute value of one of a Vector3's components.</summary>
        static ValueOutput Half(FlowGraph g, GetMember vec, string axis, Vector2 at)
        {
            var c = Get(g, typeof(Vector3), axis, at);
            Connect(g, vec.value, c.target);
            var abs = Add(g, new ScalarAbsolute { position = at + new Vector2(150, 0) });
            Connect(g, c.value, abs.input);
            var half = Add(g, new ScalarMultiply { position = at + new Vector2(300, 0) });
            Connect(g, abs.output, half.a);
            half.b.SetDefaultValue(0.5f);
            return half.product;
        }

        /// <summary>new Vector4(v.x, v.y, v.z, w) (w 0 when null).</summary>
        static ValueOutput Vec4(FlowGraph g, ValueOutput v, ValueOutput w, Vector2 at)
        {
            var x = Get(g, typeof(Vector3), "x", at);
            var y = Get(g, typeof(Vector3), "y", at + new Vector2(0, 40));
            var z = Get(g, typeof(Vector3), "z", at + new Vector2(0, 80));
            Connect(g, v, x.target);
            Connect(g, v, y.target);
            Connect(g, v, z.target);
            var ctor = Add(g, new InvokeMember(new Member(typeof(Vector4), ".ctor", new[] { typeof(float), typeof(float), typeof(float), typeof(float) })) { position = at + new Vector2(180, 0) });
            Connect(g, x.value, ctor.inputParameters[0]);
            Connect(g, y.value, ctor.inputParameters[1]);
            Connect(g, z.value, ctor.inputParameters[2]);
            if (w != null) Connect(g, w, ctor.inputParameters[3]);
            else ctor.inputParameters[3].SetDefaultValue(0f);
            return ctor.result;
        }

        static InvokeMember SetFloat(FlowGraph g, InvokeMember previous, string global, ValueOutput value, int row)
        {
            var set = Add(g, new InvokeMember(new Member(typeof(Shader), "SetGlobalFloat", new[] { typeof(string), typeof(float) })) { position = new Vector2(900, row * 90) });
            set.inputParameters[0].SetDefaultValue(global);
            Connect(g, value, set.inputParameters[1]);
            g.controlConnections.Add(new ControlConnection(previous.exit, set.enter));
            return set;
        }

        static InvokeMember SetVector(FlowGraph g, InvokeMember previous, string global, ValueOutput value, int row)
        {
            var set = Add(g, new InvokeMember(new Member(typeof(Shader), "SetGlobalVector", new[] { typeof(string), typeof(Vector4) })) { position = new Vector2(900, row * 90) });
            set.inputParameters[0].SetDefaultValue(global);
            Connect(g, value, set.inputParameters[1]);
            g.controlConnections.Add(new ControlConnection(previous.exit, set.enter));
            return set;
        }
    }
}
