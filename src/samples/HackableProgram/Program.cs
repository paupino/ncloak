using System;
using System.Configuration;
using HackableProgram.Properties;

namespace HackableProgram
{
    class Program
    {
        static void Main()
        {
            //First perform a check of the licensing key
            try
            {
                CheckRegistrationDetails();
            }
            catch (InvalidRegistrationKeyException ex)
            {
                Console.WriteLine(ex.Message);
                Environment.Exit(1);
            }

            //Display the welcome message
            Console.WriteLine("Welcome to our program!");
        }

        /// <summary>
        /// Checks the registration details.
        /// </summary>
        private static void CheckRegistrationDetails()
        {
            string value = ConfigurationManager.AppSettings["RegKey"];
            DateTime expiryDate;
            if (!DateTime.TryParse(value, out expiryDate))
                throw new InvalidRegistrationKeyException(Resources.InvalidRegistrationKey);
            if (DateTime.Now > expiryDate)
                throw new InvalidRegistrationKeyException(Resources.InvalidRegistrationKey);
            //We're ok
        }
    }
}
