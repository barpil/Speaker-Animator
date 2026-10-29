using System.Windows;

namespace SpeakerAnimator.Model.Splines;

public static class CatmullRomHelper
{
    public static List<Point> GetBezierKeyPointsFromCatmullRomKeyPoints(List<Point> points, double tension = 1, bool isClosed = false)
    {
        if (points.Count <= 2) return points;

        List<Point> cubicBezierPoints = [];
        int limit = isClosed ? points.Count : points.Count - 1;
        for (int i = 0; i < limit; i++)
        {
            Point a, b, c, d;
            b = points[i];
            c = isClosed ? points[(i + 1) % points.Count] : points[i + 1];

            if (isClosed)
            {
                a = points[(i - 1 + points.Count) % points.Count];
                d = points[(i + 2) % points.Count];
            }
            else
            {
                a = i == 0 ? b : points[i - 1];
                d = i == points.Count - 2 ? c : points[i + 2];
            }
            

            Point ra, rb, rc, rd;
            (ra, rb, rc, rd) = TransformCatmullRomKeyPointsToBezierKeyPoints(a, b, c, d, tension);
            //dla PolyBezierSegment w WPF tylko przy pierwszej iteracji sie dodaje punkt poczatkowy, bo kolejne sa rysowane od poprzedniego ostatniego elementu
            if (i == 0) cubicBezierPoints.Add(ra);
            cubicBezierPoints.Add(rb);
            cubicBezierPoints.Add(rc);
            cubicBezierPoints.Add(rd);
        }
        
        return cubicBezierPoints;
    }


    public static (Point, Point, Point, Point) TransformCatmullRomKeyPointsToBezierKeyPoints(Point a, Point b, Point c,
        Point d, double tension)
    {
        return (b, b+(c-a)/(6*tension), c-(d-b)/(6*tension), c);
    }
}