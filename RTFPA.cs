using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;

namespace FractalTracker
{
    public class RTFPA
    {
        private double minMultiplier = 0.5;
        private double maxMultiplier = 10.0;
        private bool velocityMode = false;

        Hashtable trackingStats;
        int secondsTillNewPath = 60;

        bool constrainToPlane = false;

        public RTFPA(double minMul, double maxMul)
        {
            minMultiplier = minMul;
            maxMultiplier = maxMul;
            trackingStats = new Hashtable();
        }

        public RTFPA()
        {
            trackingStats = new Hashtable();
        }

        public RunningD NewReading(string subject_id, double x, double y, double z, string timestamp)
        {
            //System.Console.WriteLine(timestamp);
            Regex rx = new Regex("([0-9]{1,2})\\s*/\\s*([0-9]{1,2}\\s*)/\\s*([0-9]{4})\\s+([0-9]{1,2}):([0-9]{1,2}):([0-9]{1,2})[:|.]([0-9]{1,3})"); //in the format dd/mm/yyyy hh:mm:ss:msm or dd/mm/yyyy hh:mm:ss.msm
            Match m = rx.Match(timestamp);

            if (!m.Success) return null;

            int day = int.Parse(m.Groups[1].ToString());
            int month = int.Parse(m.Groups[2].ToString());
            int year = int.Parse(m.Groups[3].ToString());
            int hour = int.Parse(m.Groups[4].ToString());
            int minute = int.Parse(m.Groups[5].ToString());
            int second = int.Parse(m.Groups[6].ToString());
            //string ampm = m.Groups[7].ToString();
            int millisecond = int.Parse(m.Groups[7].ToString());
            //int millisecond = 0;

            //if (ampm == "PM" && hour < 12) hour += 12;
            //If we perhaps have our day and month flipped???
            if (month > 12)
            {
                var tmp = month;
                month = day;
                day = tmp;
            }
            DateTime dt = new DateTime(year, month, day, hour, minute, second, millisecond);
             
            return NewReading(subject_id, x, y, z, dt);
        }

        public RunningD NewReading(string subject_id, double x, double y, double z, DateTime dt)
        {


            //Create a temporary RunningD for some comparisons, this can probably be eliminated at some point now that we're using a Hashtable
            RunningD temp = new RunningD(subject_id, x, y, z, dt.Ticks);


            //If this subject already exists in the list
            if (trackingStats.ContainsKey(subject_id))
            {
                //System.Console.WriteLine("Subject already exists in list");
                //Get the element in the list corresponding to the current subject
                RunningD ts = (RunningD)trackingStats[subject_id];

                //Throw out points that are in the same position as before.
                if ((!constrainToPlane && Point3D.Distance(temp.position,ts.position)==0.0) || (constrainToPlane && Point3D.XYDistance(temp.position,ts.position)==0.0)) { return ts; }

                if (Math.Abs(temp.l_timestamp - ts.l_timestamp) > secondsTillNewPath * 10000000) //Start a new path if its been more than secondsTillNewPath
                {
                    //System.Console.WriteLine("Starting a new path");
                    //System.Console.WriteLine(String.Format("{0} - {1} = {2}", temp.l_timestamp / 10000000, ts.l_timestamp / 10000000, (temp.l_timestamp - ts.l_timestamp) / 10000000));

                    //Set timestamp to new timestamp
                    ts.l_timestamp = temp.l_timestamp;

                    //Reset path lengths
                    ts.minPathLength[0] = ts.minPathLength[1] = ts.minPathLength[2] = ts.minPathLength[3] = 0.0;
                    ts.maxPathLength[0] = ts.maxPathLength[1] = ts.maxPathLength[2] = ts.maxPathLength[3] = 0.0;

                    //Reset step sizes
                    ts.minStepSize = 0;
                    ts.maxStepSize = 0;
                    ts.meanStepSize = 0;

                    ts.numberOfSteps = 0;
                    ts.realPathLength = 0;

                    ts.stepTime = 0;
                    ts.stepVelocity = 0;
                    ts.meanStepVelocity = 0;
                    ts.minStepVelocity = 0;
                    ts.maxStepVelocity = 0;
                    ts.totalStepVelocity = 0;

                    //Set the 4 sphere centers and the position history to the new point - only do this if we're starting a new path
                    for (int i = 0; i < 4; i++)
                    {
                        ts.minSphereCenter[i] = null;
                        ts.maxSphereCenter[i] = null;
                        ts.position = temp.position;
                    }

                    ts.minSphereCenter[0] = temp.position;
                    ts.maxSphereCenter[0] = temp.position;

                    //Insert ts back into the list because I think ts is a value copy, not a reference.
                    trackingStats[subject_id] = ts;
                }
                else
                {
                    //First increment the number of steps since this is a new step
                    ts.numberOfSteps++;

                    //Now calculate the time between successive readings before overwriting the timestamp in seconds
                    ts.stepTime = (temp.l_timestamp - ts.l_timestamp) / 10000000.0;
          
                    //System.Console.WriteLine("Continuing an existing path");
                    //OK so the timestamp is less than secondsTillNewPath, so set the ts timestamp to the new reading timestamp
                    ts.l_timestamp = temp.l_timestamp;
                    
                    //Now check the real distance between points and adjust our ruler accordingly.
                    //If we're constrained to a plane, zero the Z values before calculating the distance, then restore them
                    double distToLastPoint = 0.0;
                    if (constrainToPlane)
                    {
                        Point3D p1 = ts.position;
                        Point3D p2 = temp.position;
                        p1.z = p2.z = 0.0;
                        distToLastPoint = Point3D.Distance(p1, p2);
                    }
                    else distToLastPoint = Point3D.Distance(ts.position, temp.position);
                    //if (distToLastPoint < ts.minStepSize) ts.minStepSize = distToLastPoint;
                    //else if (distToLastPoint > ts.maxStepSize) ts.maxStepSize = distToLastPoint;
                    
                    //Here we recalculate the mean step size which is what we really use as a multiplication factor for the ruler
                    ts.realPathLength += distToLastPoint;
                    ts.meanStepSize = (ts.realPathLength / ts.numberOfSteps);
                    if (velocityMode) ts.meanStepSize /= ts.stepTime;//We divide by stepTime to make the min and max step sizes invariant to sensor update rate
                    ts.minStepSize = (ts.meanStepSize * minMultiplier);//But we may need to increase the multipliers because of this???
                    ts.maxStepSize = (ts.meanStepSize * maxMultiplier); 

                    /*
                    //Here we calculate the step velocities
                    ts.stepVelocity = distToLastPoint / ts.stepTime;
                    ts.totalStepVelocity += ts.stepVelocity;
                    ts.meanStepVelocity = ts.totalStepVelocity / ts.numberOfSteps;
                    ts.minStepVelocity = minMultiplier * ts.meanStepVelocity;
                    ts.maxStepVelocity = maxMultiplier * ts.meanStepVelocity;

                    //System.Console.WriteLine(ts.stepTime+"   "+ts.stepVelocity + "   " + ts.meanStepVelocity + "   " + ts.numberOfSteps);
                    */

                    //Set the most current position to the ts.position
                    ts.position = temp.position;

                    //Now if we haven't filled the sphereCenter array yet, fill it in one reading at a time up to 4 readings so we get 4 different starting points.
                    for (int i = 0; i < 4; i++)
                    {
                        if (ts.minSphereCenter[i] == null)
                        {
                            ts.minSphereCenter[i] = ts.maxSphereCenter[i] = ts.position;
                            break;
                        }
                    }
                }
            }
            else
            {
                //System.Console.WriteLine("Adding a new subject");
                trackingStats.Add(subject_id, temp); //This was a new entry from a subject_id that hasn't been seen before
            }
            //Now we can reestimate our D, we've set our new position, we've set the sphere center if necessary and have our ruler size factor.
            //Use the LineTools library.
            RunningD rd = (RunningD)trackingStats[subject_id];
            LineToolsRT.Fractal(ref rd, constrainToPlane,false);
            trackingStats[subject_id] = rd;

            return rd;  //Return the newly calculated D value
        }

        public void SetTimeout(int timeout)
        {
            secondsTillNewPath = timeout;
        }

        public void SetPlaneConstraint(bool constrain)
        {
            constrainToPlane = constrain;
        }

        public double GetMinMult()
        {
            return minMultiplier;
        }

        public double GetMaxMult()
        {
            return maxMultiplier;
        }

        public void SetVelocityMode(bool vm)
        {
            velocityMode = vm;
        }
    }
}
