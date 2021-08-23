using System;
using System.Collections.Generic;
using System.Text;

namespace FractalTracker
{
    public class Point3D
    {
        public double x;
        public double y;
        public double z;

        public Point3D()
        {
            x = y = z = 0.0;
        }

        public Point3D(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static double Distance(Point3D p1, Point3D p2)
        {
            return Math.Sqrt(Math.Pow((p1.x - p2.x), 2) + Math.Pow((p1.y - p2.y), 2) + Math.Pow((p1.z - p2.z), 2));
        }

        public static double XYDistance(Point3D p1, Point3D p2)
        {
            return Math.Sqrt(Math.Pow((p1.x - p2.x), 2) + Math.Pow((p1.y - p2.y), 2));
        }

        public override string ToString()
        {
            return String.Format("({0:0.000},{1:0.000},{2:0.000})", x, y, z);
        }

        public override bool Equals(object obj)
        {
            if(obj.GetType()!=typeof(Point3D)) return false;
            Point3D p = (Point3D)obj;
            if (p.x == this.x && p.y == this.y && p.z == this.z) return true;
            else return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}
