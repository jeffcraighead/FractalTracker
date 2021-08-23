using System;
using System.Configuration;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Specialized;


namespace FractalTracker
{
    class RTFPAProgram
    {
        static string inputfilename;
        public static string type = String.Empty;
        public static bool debug = false;
        public static string version = "0.99i";
        //static string outputfilename = "fmhi_data_" + version + ".sqlite";
        static string outputfilename = "fmhi_data_0.99.sqlite";
        private static bool quit = false;
        private static ReadingHandler rh;
        private static UbiTracker ut;
        private static UbiLogReader ulr;
        private static double minMult = 0.5;
        private static double maxMult = 10;
        public static bool velocityMode = false;
		
#if WIN32
        // Declare the SetConsoleCtrlHandler function 
        // as external and receiving a delegate.   
        [DllImport("Kernel32")]
        public static extern Boolean SetConsoleCtrlHandler(HandlerRoutine Handler,
            Boolean Add);
#endif
        // An enumerated type for the control messages 
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

		
        // A delegate type to be used as the handler routine 
        // for SetConsoleCtrlHandler.
        public delegate Boolean HandlerRoutine(CtrlTypes ctype);

        static void Main(string[] args)
        {
			
	
            // Use interop to set a console control handler.
            HandlerRoutine hr = new HandlerRoutine(CtrlMsgHandler);
#if WIN32            
			SetConsoleCtrlHandler(hr, true);
#endif

			
            //Note that there is currently no way to interrupt the CsvRead function used by UbiLogReader
            //So if you press the X or Ctrl-C during the reading of a log file nothing will happen until it gets to the processing stage.
            //At some point in the future it would be good to handle large log files in segments to avoid this issue and reduce memory requirements.

            System.Console.WriteLine("FractalTracker "+version+"\nCopyright Dr. Jeffrey Craighead 2008,2011");
            System.Console.WriteLine("--------------\n");


            /*StreamWriter sw = File.CreateText("C:\\ftdebug.txt");
            sw.WriteLine("------args------");
            foreach(string s in args) sw.WriteLine(s);
            sw.WriteLine("------AppSettings------");
            foreach (string s in System.Configuration.ConfigurationManager.AppSettings.AllKeys) sw.WriteLine(s + " ~~~ " + Convert.ToString(System.Configuration.ConfigurationManager.AppSettings[s]));
            sw.Flush();
            sw.Close();*/

            // Process our command line args before we do any initialization!
            ProcessArgs(args);

            //Check to see if the user is setting a non-default pair of multipliers
            System.Console.WriteLine("Using minMult={0:0.0} and maxMult={1:0.0}\n",minMult,maxMult);

            //Now create a reading handler since other classes will need the handle to it.
            rh = new ReadingHandler(outputfilename);

            //Check to see if we should use an input log file or listen for messages from the Ubisense services.
            //If we listen for messages, create a UbiTracker
            if (inputfilename == null)
            {
                while (!quit && ut == null)
                {
                    try
                    {
                        ut = new UbiTracker(rh,minMult,maxMult);
                    }
                    catch (UbiServicesNotAvailableException e)
                    {
                        System.Console.WriteLine(System.DateTime.Now);
                        System.Console.WriteLine(e.Message);
                        System.Console.WriteLine("Error Initializing UbiTracker - Services Must Be Down! Waiting.\n");
                        
                        ut = null;
                    }
                    Thread.Sleep(5000);
                }
            }
            else //Otherwise create a UbiLogReader
            {
                ulr = new UbiLogReader(rh, minMult, maxMult);
                quit = ulr.ProcessLogFile(inputfilename);
            }

            //Sit and wait until the program is done or the user tells us to quit. This doesn't respond during the
            //CSV reading phase if we're using a log file
            while (!quit) System.Threading.Thread.Sleep(10);
            System.Console.WriteLine("Program Complete");
            rh.CleanUpAndQuit();
            rh.logThread.Join();

            //Keep our console message handler from being garbage collected.
            GC.KeepAlive(hr);
            Environment.Exit(0);
        }

        static bool CtrlMsgHandler(CtrlTypes ctype)
        {
            System.Console.WriteLine("Quitting Now");
            if (ulr != null)
            {
                ulr.Quit();
                quit = true;
            }
            else if (ut != null)
            {
                quit = true;
            }
            else Environment.Exit(0);

            return true;
        }

        static void ProcessArgs(string[] args)
        {
            //Process AppSettings configuration file arguments.
            if (System.Configuration.ConfigurationManager.AppSettings.Count > 0)
            {
                string[] appSettings = System.Configuration.ConfigurationManager.AppSettings.AllKeys;

                for (int i = 0; i < appSettings.Length; i++)
                {
                    switch (appSettings[i].ToLower())
                    {
                        case "log":
                            inputfilename = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings[appSettings[i]]);
                            System.Console.WriteLine("Reading from log file: " + inputfilename + "\n");
                            break;
                        case "o":
                            outputfilename = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings[appSettings[i]]);
                            System.Console.WriteLine("Writing to log file: " + outputfilename + "\n");
                            break;
                        case "minmult":
                            minMult = Double.Parse(Convert.ToString(System.Configuration.ConfigurationManager.AppSettings[appSettings[i]]));
                            break;
                        case "maxmult":
                            maxMult = Double.Parse(Convert.ToString(System.Configuration.ConfigurationManager.AppSettings[appSettings[i]]));
                            break;
                        case "v":
                            velocityMode = true;
                            System.Console.WriteLine("Using Update Rate Invariant Mode.\n");
                            break;
                        case "type":
                            type = Convert.ToString(System.Configuration.ConfigurationManager.AppSettings[appSettings[i]]);
                            System.Console.WriteLine("Only recording tags with type: " + type + "\n");
                            break;
                        case "debug":
                            debug = true;
                            System.Console.WriteLine("DEBUG MODE\n");
                            break;
                    }
                }
            }

            //Process command line arguments - these override any config file parameters read in above.
            for(int i=0;i<args.Length;i++){
                switch (args[i].ToLower())
                {

                    case "-log" :
                        inputfilename = args[i + 1];
                        System.Console.WriteLine("Reading from log file: " + inputfilename + "\n");
                        i++;
                        break;
                    case "-o":
                        outputfilename = args[i + 1];
                        System.Console.WriteLine("Writing to log file: " + outputfilename + "\n");
                        i++;
                        break;
                    case "-minmult":
                        minMult = Double.Parse(args[i + 1]);
                        i++;
                        break;
                    case "-maxmult":
                        maxMult = Double.Parse(args[i + 1]);
                        i++;
                        break;
                    case "-v":
                        velocityMode = true;
                        System.Console.WriteLine("Using Update Rate Invariant Mode.\n");
                        i++;
                        break;
                    case "-type":
                        type = args[i + 1];
                        System.Console.WriteLine("Only recording tags with type: " + type + "\n");
                        i++;
                        break;
                    case "-debug":
                        debug = true;
                        System.Console.WriteLine("DEBUG MODE\n");
                        break;
                    case "?":
                    case "-?":
                        System.Console.WriteLine("Usage FMHIFractalTracker <-option> <-option> ...\n");
                        System.Console.WriteLine("Options");
                        System.Console.WriteLine("-log [file name] : Causes the tracker to read from a Ubisense Logger log file\n\tinstead of listening for messages on the network.");
                        System.Console.WriteLine("-o [file name] : Causes the tracker to write the sqlite database to the filename\n\tyou specify.");
                        System.Console.WriteLine("-minmult [min] -maxmult [max] : You must specify both of these options together.\n\tThey set the min and max multipliers used by the RTFPA algorithm.");
                        System.Console.WriteLine("-v : Causes meanStepSize to be divided by the time between readings\n\tmaking the calculation of D invariant to sensor update rate.");
                        System.Console.WriteLine("-type [type name] : Only records tags with the type specified. This option only supports a single type.");
                        System.Console.WriteLine("-debug : Displays additional debug information during operation.");
                        System.Console.WriteLine("-? : Displays this usage document.");

                        Environment.Exit(0);
                        break;

                }
            }
        }
    }
}
