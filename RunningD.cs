using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace FractalTracker
{
    public class RunningD//This struct holds the intermediate values of the D calculation
    {
        public string subject_id;

        public Point3D[] minSphereCenter;
        public Point3D[] maxSphereCenter;

        public Point3D position;

        public double[] minPathLength;
        public double[] maxPathLength;

        public double realPathLength;

        public int numberOfSteps;
        
        public double minStepSize;
        public double maxStepSize;
        public double meanStepSize;

        public long l_timestamp; //Standard DateTime timestamp, # of ticks (100ns intervals) since midnight Jan 1, 0001AD 
        public double stepTime; //The summed time successive between readings
        public double stepVelocity;
        public double totalStepVelocity;
        public double meanStepVelocity;
        public double maxStepVelocity;
        public double minStepVelocity;

        public double D; //This is the fractal dimension

        public RunningD(string subject_id, double x, double y, double z, long timestamp) //The constructor - the remaining values are set during calculations
        {
            minSphereCenter = new Point3D[4];
            maxSphereCenter = new Point3D[4];
            position = new Point3D();
            
            minPathLength = new double[4];
            maxPathLength = new double[4];

            this.subject_id = subject_id;
            
            //for (int i = 0; i < 4; i++)
            //{
            //    minSphereCenter[i] = new Point3D();
            //    maxSphereCenter[i] = new Point3D();
            //}

            position.x = x;
            position.y = y;
            position.z = z;
            
            l_timestamp = timestamp; //timestamp only holds the latest timestamp

            for (int i = 0; i < 4; i++)
            {
                minPathLength[i] = maxPathLength[i] = 0;
            }

            minSphereCenter[0] = position;
            maxSphereCenter[0] = position;

            realPathLength = 0.0;
            
            numberOfSteps = 0;
            meanStepSize = minStepSize = maxStepSize = 0;
            stepTime = 0;
            stepVelocity = 0;
            totalStepVelocity = 0;
            meanStepVelocity = 0;
            maxStepVelocity = 0;
            minStepVelocity = 0;
        }

    }

}
