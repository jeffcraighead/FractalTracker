
using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using Ubisense.UBase;
using Naming = Ubisense.UName.Naming;
using Ubisense.ULocation;
using Ubisense.ULocation.CellData;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace FractalTracker
{
    public class UbiTracker
    {
        private static Ubisense.UName.Naming.Schema naming_schema;
        private ReadingHandler rh;
        private RTFPA rt2d;

        //This constructor allows you to set the min and max multipliers passed to the RTFPA class
        public UbiTracker(ReadingHandler rh, double minMult, double maxMult)
        {
            this.rh = rh;
            rt2d = new RTFPA(minMult, maxMult);
            if (RTFPAProgram.velocityMode) rt2d.SetVelocityMode(true);
            rt2d.SetPlaneConstraint(true);
            UbiInit(rh);
        }

        //This default constructor initializes the RTFPA with its default multipliers
        public UbiTracker(ReadingHandler rh)
        {
            this.rh = rh;
            rt2d = new RTFPA();
            rt2d.SetPlaneConstraint(true);
            UbiInit(rh);
        }

        private void UbiInit(ReadingHandler rh)
        {
            
            System.Console.WriteLine("Adding MonitorMessageHandler");
            Ubisense.UBase.Monitor.MonitorMessage += new Ubisense.UBase.Monitor.MonitorMessageHandler(Monitor_MonitorMessage);

            System.Console.WriteLine("Initiating Client Connection");

            naming_schema = new Ubisense.UName.Naming.Schema(false);

            naming_schema.ConnectAsClient();

            System.Console.WriteLine("Client Connection Established");

            // create multicell object and load all Ubisense cells

            MultiCell multicell = new MultiCell();
            SortedDictionary<string, Cell> cells = multicell.GetAvailableCells();



            foreach (Cell _cell in cells.Values)
            {
                multicell.LoadCell(_cell, true);
            }



            // get all objects out of Ubisense DB
            ArrayList ObjectList = new ArrayList();
            using (ReadTransaction xact = multicell.Schema.ReadTransaction())
            {
               
                foreach (Location.RowType r in Ubisense.ULocation.CellData.Location.object_(xact))
                {
                    if (r.time_ < System.DateTime.Now - new TimeSpan(10, 0, 0, 0)) ObjectList.Add(r.object_);
                }
            }
            
            foreach (Ubisense.UBase.UObject r in ObjectList)
                multicell.RemoveObjectLocation(r);
            // definition of update event handler
            //    used if an Ubisense object moves (equivalent to old OnMove-event)
            Ubisense.ULocation.CellData.Location.AddUpdateHandler(multicell.Schema, CellData_Update);
          }
       

        /// <summary>
        /// Event-Handler of Ubisense database location update
        /// </summary>
        /// <param name="old_row"></param>
        /// <param name="new_row"></param>
        private void CellData_Update(Location.RowType old_row, Location.RowType new_row)
        {
            //Only log Person events, ignore everything else.  We may need to parameterize this later.
            if (new_row.object_.DynamicType.Name.ToUpper() != "ULOCATIONINTEGRATION::TAG")
            {
                if (RTFPAProgram.type == String.Empty || new_row.object_.DynamicType.Name.ToUpper() == RTFPAProgram.type.ToUpper()) LocationOutput(new_row);
                else if(RTFPAProgram.debug) System.Console.WriteLine("Ignoring Cell Update with DynamicType.Name==" + new_row.object_.DynamicType.Name);
            }
        }

        /// <summary>
        /// Event handler of Ubisense monitor; shows message in message box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="message"></param>
        private void Monitor_MonitorMessage(object sender, string message)
        {
            System.Diagnostics.TraceListener tl = (System.Diagnostics.TraceListener)sender;
            if (tl.IsThreadSafe)
                System.Console.WriteLine(message);
            else
            {
                System.Console.WriteLine("Monitor_MonitorMessage: "+message);


                //if (message == "configuration: Cannot find platform configuration server") throw new UbiServicesNotAvailableException(message);
               
                // kill process to exit the application
                // System.Diagnostics.Process.
                //Process p1 = System.Diagnostics.Process.GetCurrentProcess();
                //Process[] pp = Process.GetProcessesByName(p1.ProcessName);

                //foreach (Process p in pp)
                //    p.Kill();
            }
        }


        /// <summary>
        /// Output of the location of the object within Location.RowType r.
        /// </summary>
        /// <param name="r"></param>
        private void LocationOutput(Location.RowType r)
        {

            string @object = r.object_.Id.ToString();
            string name = object_name(r.object_);
            string type = r.object_.DynamicType.Name;
            double x = Math.Round(r.position_.P.X, 3);
            double y = Math.Round(r.position_.P.Y, 3);
            double z = Math.Round(r.position_.P.Z, 3);
          //  string datetime1 = r.time_.ToUniversalTime().ToString();//r.time_.Year +"-"+r.time_.Month + "-" + r.time_.Day + " " + r.time_.Hour + ":" + r.time_.Minute + ":" + r.time_.Second + ":" + r.time_.Millisecond;
            string dt = String.Format("{0:00}/{1:00}/{2:0000} {3:00}:{4:00}:{5:00}.{6:000}", r.time_.Day, r.time_.Month, r.time_.Year, r.time_.Hour, r.time_.Minute, r.time_.Second, r.time_.Millisecond);

          //  if(r.time_!=null)System.Console.WriteLine(String.Format("Ubisense DateTime Kind = {0}",r.time_.Kind));

            RunningD rd = rt2d.NewReading(name, x, y, z, r.time_);

            System.Console.WriteLine(name + " " + type + " x:" + x + ", y:" + y + ", z:" + z + " Time:" + dt + " D2:" + rd.D);
            
            //Calculate the mean min and max path lengths
            double minPathLen = 0.0;
            double maxPathLen = 0.0;
            for (int i = 0; i < 4; i++)
            {
                minPathLen += rd.minPathLength[i];
                maxPathLen += rd.maxPathLength[i];
            }           
            minPathLen /= 4.0;
            maxPathLen /= 4.0;

            rh.LogEvent(name, x, y, z, rd.D, dt, rd.meanStepSize, rd.numberOfSteps, rt2d.GetMinMult(), rt2d.GetMaxMult()); //Timestamp has no whitespace so database can be sorted easier.
            if(rh.queueCount>100) rh.FlushQueue();
        }

        /// <summary>
        /// Gets the name of the given object. 
        /// </summary>
        /// <param name="obj">UObject</param>
        /// <returns>String with name of the object obj</returns>
        protected static string object_name(UObject obj)
        {   //If the object has a name in Site Manager
            if (naming_schema != null)
                using (Ubisense.UName.Naming.ReadTransaction xact = naming_schema.ReadTransaction())
                    foreach (Ubisense.UName.Naming.ObjectName.RowType row in Ubisense.UName.Naming.ObjectName.object_name_(xact, obj))
                        return row.name_;

            //If the object is a tag
            if (obj.DynamicType.Name == "ULocationIntegration::Tag")
            {
                Ubisense.ULocationIntegration.Tag tag = new Ubisense.ULocationIntegration.Tag();
                tag.Narrow(obj);
                return Ubisense.ULocationIntegration.Tag.ConvertIdToString(tag.PhysicalId, '-');

            }

            //If the object is unamed
            return "(unnamed " + obj.DynamicType + ")";
        }
    }

    [Serializable]
    public class UbiServicesNotAvailableException : System.Exception
    {
        public UbiServicesNotAvailableException()
        {
        }

        public UbiServicesNotAvailableException(string message)
            : base(message)
        {
        }

        public UbiServicesNotAvailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected UbiServicesNotAvailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
