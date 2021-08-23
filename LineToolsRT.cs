using System;
using System.Collections;


namespace FractalTracker
{
    public class LineToolsRT
    {
        public static Point3D[] LineSphereIntersect(Point3D linePoint1, Point3D linePoint2, Point3D sphereCenter, double sphereRadius, bool constrainToSegment)
        { //Determines where on the specified line segment the specified sphere intersects
            //First check to see if the segment intersects the sphere at all
            //double u = ((sphereCenter.x - linePoint1.x) * (linePoint2.x - linePoint1.x) + (sphereCenter.y - linePoint1.y) * (linePoint2.y - linePoint1.y) + (sphereCenter.z - linePoint1.z) * (linePoint2.z - linePoint1.z));
            //u /= ((linePoint2.x - linePoint1.x) * (linePoint2.x - linePoint1.x) + (linePoint2.y - linePoint1.y) * (linePoint2.y - linePoint1.y) + (linePoint2.z - linePoint1.z) * (linePoint2.z - linePoint1.z));

            //If the line segment doesn't intersect the sphere, no need to calculate the intersection!
            //if (!(u >= 0 && u <= 1)) return null;

            //Solutions are of the form -b +- sqrt(b^2 - 4ac)/2a where

            double a = Math.Pow((linePoint2.x - linePoint1.x), 2.0) + Math.Pow((linePoint2.y - linePoint1.y), 2.0) + Math.Pow((linePoint2.z - linePoint1.z), 2.0);
            double b = 2 * ((linePoint2.x - linePoint1.x) * (linePoint1.x - sphereCenter.x) + (linePoint2.y - linePoint1.y) * (linePoint1.y - sphereCenter.y) + (linePoint2.z - linePoint1.z) * (linePoint1.z - sphereCenter.z));
            double c = Math.Pow(sphereCenter.x, 2.0) + Math.Pow(sphereCenter.y, 2.0) + Math.Pow(sphereCenter.z, 2.0) + Math.Pow(linePoint1.x, 2.0) + Math.Pow(linePoint1.y, 2.0) + Math.Pow(linePoint1.z, 2.0) - 2 * (sphereCenter.x * linePoint1.x + sphereCenter.y * linePoint1.y + sphereCenter.z * linePoint1.z) - Math.Pow(sphereRadius, 2.0);

            double inner = (b * b - 4 * a * c);

            Point3D[] result = null;

            // Debug.Log("inner: " + inner);

            if (inner < 0) return null; //no intersection WTF, didn't we check for this above?
            else if (inner == 0)
            { //line is tangent to sphere at u=-b/2a
                result = new Point3D[1];
                result[0] = new Point3D();
                result[0].x = linePoint1.x + (-b / (2 * a)) * (linePoint2.x - linePoint1.x);
                result[0].y = linePoint1.y + (-b / (2 * a)) * (linePoint2.y - linePoint1.y);
                result[0].z = linePoint1.z + (-b / (2 * a)) * (linePoint2.z - linePoint1.z);

                return result;
            }
            else
            {

                result = new Point3D[2];
                double solution1 = (-b + Math.Sqrt(inner)) / (2 * a);
                double solution2 = (-b - Math.Sqrt(inner)) / (2 * a);

                if (constrainToSegment)
                {
                    if (solution1 < 0) solution1 = 0;
                    else if (solution1 > 1) solution1 = 1;

                    if (solution2 < 0) solution2 = 0;
                    else if (solution2 > 1) solution2 = 1;
                }

                //Debug.Log("s1: " + solution1 + "   s2: "  + solution2);
                result[0] = new Point3D();
                result[0].x = linePoint1.x + (solution1) * (linePoint2.x - linePoint1.x);
                result[0].y = linePoint1.y + (solution1) * (linePoint2.y - linePoint1.y);
                result[0].z = linePoint1.z + (solution1) * (linePoint2.z - linePoint1.z);
               
                result[1] = new Point3D();
                result[1].x = linePoint1.x + (solution2) * (linePoint2.x - linePoint1.x);
                result[1].y = linePoint1.y + (solution2) * (linePoint2.y - linePoint1.y);
                result[1].z = linePoint1.z + (solution2) * (linePoint2.z - linePoint1.z);

                return result;
            }
        }

        public static void Fractal(ref RunningD r, bool constrainToPlane, bool useVelocity)
        { //path is a list of points that make up the path, scale is the scale in meters with which to measure the length, reverse indicates that we will measure from the end of the path instead of the beginning. (for Fractal D analysis)
            double runningTotal = 0.0;
            Point3D[] intersectPoints;
            Point3D newPoint = r.position;
            double[] dist = new double[4];
            double minScale = r.minStepSize;
            double maxScale = r.maxStepSize;

            if (useVelocity)
            {
                minScale = r.minStepVelocity;
                maxScale = r.maxStepVelocity;
            }

            //If we only want to calculate on the XY plane then set newPoint.z = 0;
            if (constrainToPlane) newPoint.z = 0;

            //Calculate the four min path lengths
            for (int i = 0; i < 4; i++)
            {
                if (r.minSphereCenter[i] == null) break;
                if (minScale<=0.0 || Point3D.Distance(r.minSphereCenter[i], newPoint) < minScale) continue; //If our scale is much larger than the distance between waypoints we want to skip to a waypoint that is measurable using our scale
                intersectPoints = LineSphereIntersect(r.minSphereCenter[i], newPoint, r.minSphereCenter[i], minScale, true);//Get the potential intersection points for the walk of the path
                if (intersectPoints == null) continue;

                //Now determine which potential point moves us in the desired direction on the path (the point closer to path[i+step]).
                double d0 = Point3D.Distance(intersectPoints[0], newPoint);
                double d1 = Point3D.Distance(intersectPoints[1], newPoint);
                if (d0 < d1)
                {
                    runningTotal += minScale;
                    r.minSphereCenter[i] = intersectPoints[0];
                    // Debug.Log("Moving Center d0, i="+i + " :: " + d0);
                }
                else
                {
                    runningTotal += minScale;
                    r.minSphereCenter[i] = intersectPoints[1];
                    // Debug.Log("Moving Center d0, i="+i + " :: " + d1);
                }
                // Debug.Log("i="+i);
                if (Point3D.Distance(r.minSphereCenter[i], newPoint) >= minScale) i -= 1; //We only want to measure to the next waypoint if we are within scale distance of that waypoint, otherwise move along.
                else
                {
                    r.minPathLength[i] += runningTotal;
                    runningTotal = 0;
                }


                //Now calculate the four fractal Ds.

            }
            //Calculate the four maxPathLengths
            for (int i = 0; i < 4; i++)
            {
                if (r.maxSphereCenter[i] == null) break;
                if (maxScale<=0.0 || Point3D.Distance(r.maxSphereCenter[i], newPoint) < maxScale) continue; //If our scale is much larger than the distance between waypoints we want to skip to a waypoint that is measurable using our scale
                intersectPoints = LineSphereIntersect(r.maxSphereCenter[i], newPoint, r.maxSphereCenter[i], maxScale, true);//Get the potential intersection points for the walk of the path
                if (intersectPoints == null) continue;

                //Now determine which potential point moves us in the desired direction on the path (the point closer to path[i+step]).
                double d0 = Point3D.Distance(intersectPoints[0], newPoint);
                double d1 = Point3D.Distance(intersectPoints[1], newPoint);
                if (d0 < d1)
                {
                    runningTotal += maxScale;
                    r.maxSphereCenter[i] = intersectPoints[0];
                    // Debug.Log("Moving Center d0, i="+i + " :: " + d0);
                }
                else
                {
                    runningTotal += maxScale;
                    r.maxSphereCenter[i] = intersectPoints[1];
                    // Debug.Log("Moving Center d0, i="+i + " :: " + d1);
                }
                // Debug.Log("i="+i);
                if (Point3D.Distance(r.maxSphereCenter[i], newPoint) >= maxScale) i -= 1; //We only want to measure to the next waypoint if we are within scale distance of that waypoint, otherwise move along.
                else
                {
                    r.maxPathLength[i] += runningTotal;
                    runningTotal = 0;
                }
            }
            double fd1 = 1.0f - (Math.Log10(r.minPathLength[0]) - Math.Log10(r.maxPathLength[0])) / (Math.Log10(minScale) - Math.Log10(maxScale));  //calculating fracD here based on Nams' description of 1-D=slope of plot log(path length) vs. log(step size)
            double fd2 = 1.0f - (Math.Log10(r.minPathLength[1]) - Math.Log10(r.maxPathLength[1])) / (Math.Log10(minScale) - Math.Log10(maxScale));
            double fd3 = 1.0f - (Math.Log10(r.minPathLength[2]) - Math.Log10(r.maxPathLength[2])) / (Math.Log10(minScale) - Math.Log10(maxScale));
            double fd4 = 1.0f - (Math.Log10(r.minPathLength[3]) - Math.Log10(r.maxPathLength[3])) / (Math.Log10(minScale) - Math.Log10(maxScale));

            //System.Console.WriteLine(String.Format("fd1={0:0.00}   fd2={1:0.00}   fd3={2:0.00}   fd4={3:0.00}\n-------",fd1,fd2,fd3,fd4));

            //Now place the mean4 D value in r.  Only average non NaN values into the mean4 (so sometimes it's not actually a mean4 D)
            double temp = 0.0;
            int count=0;
            if (!Double.IsNaN(fd1) && !Double.IsInfinity(fd1)) { temp += fd1; count++; }
            if (!Double.IsNaN(fd2) && !Double.IsInfinity(fd2)) { temp += fd2; count++; }
            if (!Double.IsNaN(fd3) && !Double.IsInfinity(fd3)) { temp += fd3; count++; }
            if (!Double.IsNaN(fd4) && !Double.IsInfinity(fd4)) { temp += fd4; count++; }
            if (count > 0) r.D = temp / count;
            else r.D = 0;
            //if (Double.IsInfinity(r.D)) System.Console.WriteLine(String.Format("fd1={0:0.00}   fd2={1:0.00}   fd3={2:0.00}   fd4={3:0.00}\n-------", fd1, fd2, fd3, fd4));
        }

    }
}
