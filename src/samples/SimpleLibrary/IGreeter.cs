namespace SimpleLibrary
{
    /// <summary>
    /// This serves as an interface for the Greeter objects.
    /// This demonstrates a public access interface to ensure that public members here are translated appropriately
    /// in deriving classes
    /// </summary>
    public interface IGreeter
    {
        /// <summary>
        /// Returns the greeting for the given name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>The greeting text in the appropriate language</returns>
        string Greet(string name);
    }
}
