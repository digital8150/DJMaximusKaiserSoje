using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using DJMaximusKaiserSoje.Core;
using System.Collections.Generic;

namespace DJMaximusKaiserSoje.Gameplay
{
    public interface ILaneInput : IDisposable
    {
        event Action<int, double> Pressed;
        event Action<int, double> Released;

        void Enable();
        void Disable();
    }

    /// <summary>
    /// Owns only keyboard actions for the current lane layout. Input timestamps are left in the
    /// Input System's clock domain; PlaySession translates them into the DSP domain.
    /// </summary>
    public sealed class LaneInput : ILaneInput
    {
        private readonly InputAction[] actions;
        private bool disposed;

        public LaneInput(LaneLayout layout, IReadOnlyList<string> keyBindings = null)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            string[] bindings = GameOptionRules.NormalizeBindings(layout.Style, keyBindings);
            actions = new InputAction[layout.Lanes.Count];
            for (int index = 0; index < layout.Lanes.Count; index++)
            {
                int capturedIndex = index;
                string control = bindings[index].ToLowerInvariant();
                var action = new InputAction("Lane" + index, InputActionType.Button, "<Keyboard>/" + control);
                action.performed += context => Pressed?.Invoke(capturedIndex, context.time);
                action.canceled += context => Released?.Invoke(capturedIndex, context.time);
                actions[index] = action;
            }
        }

        public event Action<int, double> Pressed;
        public event Action<int, double> Released;

        public event Action<int, double> LanePressed
        {
            add => Pressed += value;
            remove => Pressed -= value;
        }

        public event Action<int, double> LaneReleased
        {
            add => Released += value;
            remove => Released -= value;
        }

        public int LaneCount => actions.Length;

        public void Enable()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LaneInput));
            for (int index = 0; index < actions.Length; index++) actions[index].Enable();
        }

        public void Disable()
        {
            if (disposed) return;
            for (int index = 0; index < actions.Length; index++) actions[index].Disable();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = 0; index < actions.Length; index++) actions[index].Dispose();
        }
    }

    public sealed class InputSystemTimeSource : IInputTimeSource
    {
        public double CurrentTime => InputState.currentTime;
    }
}
