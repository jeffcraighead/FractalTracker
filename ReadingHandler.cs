using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;

using System.Data.SQLite;

using System.Threading;
using System.Data.Common;

namespace FractalTracker
{
    public class ReadingHandler
    {
        Queue<String[]> logQueue; //This queue holds incoming readings (in the form of SQL command strings) that need to be written to the database
        SQLiteConnection sql;
        public Thread logThread;
        bool runLogThread = true;

        bool flush = false;
        
        public int queueCount = 0;

        private static Mutex mut;

        public ReadingHandler(string filename)
        {
            logQueue = new Queue<String[]>();
            string connectionString = "Data Source=" + filename + ";Synchronous=OFF;";
            this.sql = new SQLiteConnection(connectionString);
            sql.Open();

            //Create the location readings table
            SQLiteCommand sqc = sql.CreateCommand();
            sqc.CommandText = "CREATE TABLE IF NOT EXISTS LOCATION_READINGS(IDX integer primary key autoincrement, ID string, X string, Y string, Z string, D string, TIME string, LOCALTIME string, LONGTIME integer, MEANSTEP string, NUMSTEPS string, MINMULT string, MAXMULT string)";
            int rows = sqc.ExecuteNonQuery();

            //Create index on IDs of location_readings
            sqc.CommandText = "CREATE INDEX IF NOT EXISTS ID_INDEX ON LOCATION_READINGS(ID)";
            rows = sqc.ExecuteNonQuery();

            //Create the log info table
            sqc.CommandText = "CREATE TABLE IF NOT EXISTS LOG_START_INFO(DATE string, VERSION string)";
            rows = sqc.ExecuteNonQuery();

            //Create table to show last seen dates
            sqc.CommandText = "CREATE TABLE IF NOT EXISTS LAST_SEEN(ID string, DATE string, D string)";
            rows = sqc.ExecuteNonQuery();

            //Create table to keep end of path info
            sqc.CommandText = "CREATE TABLE IF NOT EXISTS PATH_D_VALUES(ID string, DATE string, D string)";
            rows = sqc.ExecuteNonQuery();

            //Now enter the info into the log info table
            DateTime t = DateTime.Now;
           // if (t != null) System.Console.WriteLine(String.Format("ReadingHandler DateTime Kind = {0}", t.Kind));

            //string datetime = String.Format("{0:00}/{1:00}/{2:0000} {3:00}:{4:00}:{5:00}.{6:000}", t.Day, t.Month, t.Year, t.Hour, t.Minute, t.Second, t.Millisecond);
            
            sqc.CommandText = String.Format("INSERT INTO LOG_START_INFO(DATE,VERSION) VALUES('{0}','{1}')",t,RTFPAProgram.version);
            sqc.ExecuteNonQuery();

            sqc.Dispose();

            mut = new Mutex(); //Create the mutex that we'll use to sync the following thread
            logThread = new Thread(new ThreadStart(LogThreadTask)); //This thread writes to the SQLite Db
            logThread.Start();
        }

        public void LogEvent(string subject_id, double x, double y, double z, double d2d, string time, double meanStep, double numsteps, double minMult, double maxMult)
        {
            

            DateTime t = DateTime.Now;
            string year = t.Year.ToString("d4");
            string month = t.Month.ToString("d2");
            string day = t.Day.ToString("d2");
            string hour = t.Hour.ToString("d2");
            string minute = t.Minute.ToString("d2");
            string second = t.Second.ToString("d2");
            string millis = t.Millisecond.ToString("d3");

            string datetime = String.Format("{0}/{1}/{2} {3}:{4}:{5}.{6}", day, month, year, hour, minute, second, millis);
            string i_datetime = String.Format("{0}{1}{2}{3}{4}{5}{6}", year, month, day, hour, minute, second, millis);
			
            String[] entry = new String[12];
            entry[0] = subject_id;
            entry[1] = String.Format("{0:0.000}", x);
            entry[2] = String.Format("{0:0.000}", y);
            entry[3] = String.Format("{0:0.000}", z);
            entry[4] = String.Format("{0:0.000}", d2d);
            entry[5] = time;
            entry[6] = String.Format("{0:0.000}", meanStep);
            entry[7] = String.Format("{0:0.000}", numsteps);
            entry[8] = String.Format("{0:0.00}", minMult);
            entry[9] = String.Format("{0:0.00}", maxMult);
            entry[10] = datetime;
            entry[11] = i_datetime;

           // String.Format("INSERT OR IGNORE INTO LOCATION_READINGS(SUBJECT, X, Y, Z, D, TIME ,MEANSTEP, NUMSTEPS, LOCALTIME) VALUES ('{0}','{1:0.000}','{2:0.000}','{3:0.000}','{4:0.000}','{5}','{6:0.000}','{7}','{8}')", subject_id, x, y, z, d2d, time, meanStep, numsteps, datetime);

            /*
            if (numsteps == 0)
            {
                mut.WaitOne();
                DbTransaction dbTrans = sql.BeginTransaction();
                DbCommand cmd = sql.CreateCommand();
                cmd.CommandText = String.Format("INSERT INTO PATH_D_VALUES(ID, DATE, D) VALUES()", subject_id, datetime, d2d);
                cmd.ExecuteNonQuery();
                mut.ReleaseMutex();
            }
            
            */

            mut.WaitOne(); //Lock access to logQueue using mut
            logQueue.Enqueue(entry);
            mut.ReleaseMutex(); //Release the lock on logQueue
            queueCount = logQueue.Count;
        }

        //This function which simply writes readings to the database on disk runs in a separate thread. The incoming readings are stored in logQueue until they are written.
        //This along with bulk writes to the Db gave a 10x performance increase and prevents any clobbering of incoming readings.
        public void LogThreadTask()
        {
            String[] entry;
            while (runLogThread || logQueue.Count>0)
            {
                if (logQueue.Count > 100 || flush == true || runLogThread==false)
                {
                    mut.WaitOne();
                    using (DbTransaction dbTrans = sql.BeginTransaction())
                    {
                        using (DbCommand cmd = sql.CreateCommand())
                        {
                            cmd.CommandText = "INSERT OR IGNORE INTO LOCATION_READINGS(ID, X, Y, Z, D, TIME ,MEANSTEP, NUMSTEPS, LOCALTIME, MINMULT, MAXMULT, LONGTIME) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)";
                            DbParameter sub = cmd.CreateParameter(); //subject
                            DbParameter x = cmd.CreateParameter(); //x position
                            DbParameter y = cmd.CreateParameter(); //y position
                            DbParameter z = cmd.CreateParameter(); //z position
                            DbParameter d = cmd.CreateParameter(); //D value
                            DbParameter t = cmd.CreateParameter(); //Ubi time stamp
                            DbParameter ms = cmd.CreateParameter(); //mean step size
                            DbParameter ns = cmd.CreateParameter(); //number of steps
                            DbParameter st = cmd.CreateParameter(); //system time stamp
                            DbParameter minM = cmd.CreateParameter(); //min multiplier
                            DbParameter maxM = cmd.CreateParameter(); //max multiplier
                            DbParameter lt = cmd.CreateParameter(); //long format system time stamp
                            
                            cmd.Parameters.Add(sub);
                            cmd.Parameters.Add(x);
                            cmd.Parameters.Add(y);
                            cmd.Parameters.Add(z);
                            cmd.Parameters.Add(d);
                            cmd.Parameters.Add(t);
                            cmd.Parameters.Add(ms);
                            cmd.Parameters.Add(ns);
                            cmd.Parameters.Add(st);
                            cmd.Parameters.Add(minM);
                            cmd.Parameters.Add(maxM);
                            cmd.Parameters.Add(lt);
                            
                            
                            for (int i = 0; i < logQueue.Count; i++)
                            {
                                entry = logQueue.Dequeue();
                                sub.Value = entry[0];
                                x.Value = entry[1];
                                y.Value = entry[2];
                                z.Value = entry[3];
                                d.Value = entry[4];
                                t.Value = entry[5];
                                ms.Value = entry[6];
                                ns.Value = entry[7];                                
                                minM.Value = entry[8];
                                maxM.Value = entry[9];
                                st.Value = entry[10];
                                lt.Value = Int64.Parse(entry[11]);
                                cmd.ExecuteNonQuery();
                            }

                            dbTrans.Commit();
                        }
                    }
                    flush = false;
                    mut.ReleaseMutex();
                    System.Console.WriteLine("LogQueue just flushed, {0} entries left in the queue", logQueue.Count);
                }
                else Thread.Sleep(1000);
            }
            System.Console.WriteLine("Exiting LogThread Now");
        }

        public void FlushQueue()
        {
            flush = true;
        }

        public void CleanUpAndQuit()
        {
            FlushQueue();
            runLogThread = false;
            System.Console.WriteLine("Done Cleaning Reading Handler Now!");
        }
    }
}
