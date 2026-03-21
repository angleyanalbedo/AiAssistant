using System;

namespace AiAssistant.UIControls.Utils
{
    public class InsertCodeEventArgs : EventArgs
    {
        public string Code { get; set; }
        public bool ReplaceSelection { get; set; }
    }
}
