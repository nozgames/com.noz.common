using System;

namespace NoZ.Stylez
{
    public interface IStylezStateProvider
    {
        StylezState GetState();

        void SetStateChangedCallback(Action<StylezState> callback);
    }
}
