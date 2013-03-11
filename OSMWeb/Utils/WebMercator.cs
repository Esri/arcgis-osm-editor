using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OSMWeb.Utils
{
    public class WebMercator
    {
        private static double DEGREES_PER_RADIANS = 57.295779513082320;
        private static double RADIANS_PER_DEGREES = 0.017453292519943;
        private static double PI_OVER_2 = Math.PI / 2.0;

        private static double RADIUS = 6378137; // Using Equatorial radius: http://en.wikipedia.org/wiki/Earth_radius

        public static int WebMercatorWKID = 100102;

        /// <summary>
        /// From WGS84 to WebMercator
        /// </summary>
        /// <param name="latitude"></param>
        /// <returns></returns>
        public static double ToWebMercatorY(double latitude)
        {
            double rad = latitude * RADIANS_PER_DEGREES;
            double sin = Math.Sin(rad);
            double y = RADIUS / 2.0 * Math.Log((1.0 + sin) / (1.0 - sin));
            return y;
        }

        /// <summary>
        ///  From WGS84 to WebMercator
        /// </summary>
        /// <param name="longitude"></param>
        /// <returns></returns>
        public static double ToWebMercatorX(double longitude)
        {
            double x = longitude * RADIANS_PER_DEGREES * RADIUS;
            return x;
        }

       

        /// <summary>
        /// From WebMercacor 102100 to WGS84
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static double FromWebMercatorX(double x)
        {
            double rad = x / RADIUS;
            double deg = rad * DEGREES_PER_RADIANS;
            double rot = Math.Floor((deg + 180) / 360);
            double lon = deg - (rot * 360);
            return lon;
        }

        /// <summary>
        /// From WebMercacor 102100 to WGS84
        /// </summary>
        /// <param name="y"></param>
        /// <returns></returns>
        public static double FromWebMercatorY(double y)
        {
            double rad = PI_OVER_2 - (2.0 * Math.Atan(Math.Exp(-1.0 * y / RADIUS)));
            double lat = rad * DEGREES_PER_RADIANS;
            return lat;
        }
    }
}