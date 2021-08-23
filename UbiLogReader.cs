using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Text.RegularExpressions;

namespace FractalTracker
{
    class UbiLogReader
    {
        private ReadingHandler rh;
        private RTFPA rt2d;
        private DataTable csvdata;
        private CsvDataSource.CsvDataSource csvds;
        private long numRecords = 0;
        private bool quit = false;

        public UbiLogReader(ReadingHandler rh, double minMul, double maxMul)
        {
            this.rh = rh;
            rt2d = new RTFPA(minMul,maxMul);
            if (RTFPAProgram.velocityMode) rt2d.SetVelocityMode(true);
            rt2d.SetPlaneConstraint(true);
        }
        
        public UbiLogReader(ReadingHandler rh)
        {
            this.rh = rh;
            rt2d = new RTFPA();
            rt2d.SetPlaneConstraint(true);           
        }

        public void Quit()
        {
            System.Console.WriteLine("ULR.Quit was called!");
            quit = true;
        }

        public bool ProcessLogFile(string csvfilepath)
        {
            csvds = new CsvDataSource.CsvDataSource();

            System.Console.WriteLine("Reading File Contents Now. This could take a while.");
            csvdata = csvds.ReadFile(csvfilepath);

            System.Console.WriteLine("Processing Data Now. This will take even longer.");
            
            foreach (DataRow row in csvdata.Rows)
            {
                if(row!=null) LocationOutput(row);
                if (quit) break;
            }

            //OK all events in the file should be in the RH.logQueue, flush it now.
            rh.FlushQueue();
            System.Console.WriteLine("Done Reading File");
            return true;
        }

        private void LocationOutput(DataRow r)
        {
            string name;
            string datetime1;
            double x;
            double y;
            double z;
            try
            {
                name = (string)r[1];
                datetime1 = (string)r[2];
                x = Double.Parse((string)r[3]);
                y = Double.Parse((string)r[4]);
                z = Double.Parse((string)r[5]);
            }
            catch (System.InvalidCastException) { return; }

            string dt = "";
            Regex rx = new Regex("([0-9]{1,2})\\s*/\\s*([0-9]{1,2})\\s*/\\s*([0-9]{4})\\s*([0-9]{1,2}):([0-9]{1,2}):([0-9]{1,2})[:|.]([0-9]{1,3})"); //in the format dd/mm/yyyy hh:mm:ss:msm or dd/mm/yyyy hh:mm:ss.msm
            Match m = rx.Match(datetime1);

            if (m.Success)
            {
                int day = int.Parse(m.Groups[1].ToString());
                int month = int.Parse(m.Groups[2].ToString());
                int year = int.Parse(m.Groups[3].ToString());
                int hour = int.Parse(m.Groups[4].ToString());
                int minute = int.Parse(m.Groups[5].ToString());
                int second = int.Parse(m.Groups[6].ToString());
                int millisecond = int.Parse(m.Groups[7].ToString());

                dt = String.Format("{0:00}/{1:00}/{2:0000} {3:00}:{4:00}:{5:00}.{6:000}", day, month, year, hour, minute, second, millisecond);

            }

            RunningD rd = rt2d.NewReading(name, x, y, z, datetime1);

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

            //Log the processed event to the database.
            rh.LogEvent(name, x, y, z, rd.D, dt, rd.meanStepSize, rd.numberOfSteps, rt2d.GetMinMult(), rt2d.GetMaxMult()); //Timestamp has no whitespace so database can be sorted easier.
            
            numRecords++;

            //Only output to screen every 1000 records for speed.
            if (numRecords % 1000 == 0)
            {
                System.Console.WriteLine("Processing Record " + numRecords);
                System.Console.WriteLine(name + " x:" + x + ", y:" + y + ", z:" + z + " Time:" + dt + " D2:" + rd.D);
            }
        }
    }
}
