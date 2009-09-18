using System;

namespace SimpleLibrary
{
    /// <summary>
    /// A simple implementation of the greeter object demonstrating public members 
    /// within an interface environment
    /// </summary>
    public class EnglishGreeter : IGreeter
    {
        public string Greet(string name)
        {
            return String.Format("Hello {0}", name);
        }
    }
}
