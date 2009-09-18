using System;
using System.Diagnostics;
using NUnit.Framework;
using System.IO;

namespace TiviT.NCloak.Tests
{
    /// <summary>
    /// Tests in this class ensure that the output before and after obfuscation is the same
    /// </summary>
    [TestFixture]
    public class ProgramExecutionResults
    {
        /// <summary>
        /// This test ensures the program output is correct before obfuscation
        /// </summary>
        [Test]
        public void PreObfuscationProgramOutputIsCorrect()
        {
            //Execute the program in standard format
            RunProgramAndCheckProgramOutputIsCorrect("SimpleProgram.exe");
        }

        /// <summary>
        /// This test ensures that after obfuscation, the program output is still correct
        /// </summary>
        [Test]
        public void PostObfuscationProgramOutputIsCorrect()
        {
            //Obfuscate first
            ObfuscationHelper.Obfuscate("SimpleProgram.exe", "SimpleLibrary.dll");

            //Run the obfuscated version...
            RunProgramAndCheckProgramOutputIsCorrect("Obfuscated\\SimpleProgram.exe");
        }

        /// <summary>
        /// Runs the program and check program output is correct.
        /// </summary>
        /// <param name="program">The program.</param>
        public void RunProgramAndCheckProgramOutputIsCorrect(string program)
        {
            //First off start the process
            ProcessStartInfo processStartInfo = new ProcessStartInfo(program);
            processStartInfo.CreateNoWindow = true;
            processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.UseShellExecute = false;
            Process process = Process.Start(processStartInfo);

            //Do some assertions and checks!
            //First check that our process is not null
            Assert.That(process, Is.Not.Null, "Process must not be null");

            //The next line is to disable resharper warnings for this next section
            // ReSharper disable PossibleNullReferenceException
            StreamReader stdOut = process.StandardOutput;
            StreamWriter stdIn = process.StandardInput;
            // ReSharper restore PossibleNullReferenceException

            //Check the intro text is correct
            Assert.That(stdOut.ReadLine(), Is.EqualTo("Starting Command Line Service..."),
                        "Introduction text");
            //Ignore the next 2 lines - but just check that they aren't null
            Assert.That(stdOut.ReadLine(), Is.Not.Empty, "Rubbish line 1");
            Assert.That(stdOut.ReadLine(), Is.Not.Empty, "Rubbish line 2");
            //Check the registered commands text
            Assert.That(stdOut.ReadLine(), Is.EqualTo("Registered commands are:"), "Registered commands introduction");
            Assert.That(stdOut.ReadLine(), Is.EqualTo("quit hello "), "Registered commands");

            //Make sure that the prompt is displayed
            char[] promptBuffer = new char[2];
            stdOut.Read(promptBuffer, 0, 2);
            Assert.That(new String(promptBuffer), Is.EqualTo("> "), "Prompt 1");

            //Send through the hello command
            stdIn.WriteLine("hello");
            
            //Check the results
            Assert.That(stdOut.ReadLine(), Is.EqualTo("Hello " + Environment.UserName), "Hello output");
            Assert.That(stdOut.ReadLine(), Is.EqualTo("Command completed successfully"), "Hello command successful");
            
            //Check the prompt again
            stdOut.Read(promptBuffer, 0, 2);
            Assert.That(new String(promptBuffer), Is.EqualTo("> "), "Prompt 2");

            //Send through the quit command
            stdIn.WriteLine("quit");
            
            //Check the results
            Assert.That(stdOut.ReadLine(), Is.EqualTo("Command completed successfully"), "Hello command successful");

            //Done! Ensure that the program is killed
            if (!process.HasExited)
                process.Kill();
        }
    }
}
