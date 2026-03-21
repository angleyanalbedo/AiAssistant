using System;

namespace AiAssistant.UIControls.Utils
{
    public class AiActionRequestedEventArgs : EventArgs
    {
        public string Prompt { get; set; }
    }
}
