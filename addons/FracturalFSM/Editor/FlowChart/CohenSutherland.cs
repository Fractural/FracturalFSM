using Godot;

namespace Fractural.FlowChart
{
    public static class CohenSutherland
    {
        const int INSIDE = 0;// 0000
        const int LEFT = 1;// 0001
        const int RIGHT = 2;// 0010
        const int BOTTOM = 4;// 0100
        const int TOP = 8;// 1000

        // Compute bit code for a Point(x, y) using the clip
        public static int ComputeCode(float x, float y, float xMin, float yMin, float xMax, float yMax)
        {
            var code = INSIDE;// initialised as being inside of clip window
            if (x < xMin) // to the left of clip window
            {
                code |= LEFT;
            }
            else if (x > xMax) // to the right of clip window
            {
                code |= RIGHT;
            }
            if (y < yMin) // below the clip window
            {
                code |= BOTTOM;
            }
            else if (y > yMax) // above the clip window
            {
                code |= TOP;
            }
            return code;

            // Cohen-Sutherland clipping algorithm clips a line from
            // P0 = (x0, y0) to P1 = (x1, y1) against a rectangle with
            // diagonal from Start(xMin, yMin) to End(xMax, yMax)
        }

        public static bool LineIntersectRectangle(Vector2 from, Vector2 to, Rect2 rect)
        {
            var xMin = rect.Position.x;
            var yMin = rect.Position.y;
            var xMax = rect.End.x;
            var yMax = rect.End.y;

            var code0 = ComputeCode(from.x, from.y, xMin, yMin, xMax, yMax);
            var code1 = ComputeCode(to.x, to.y, xMin, yMin, xMax, yMax);

            int i = 0;
            while (true)
            {
                i += 1;
                if (!((code0 | code1) > 0)) // bitwise OR 0, both points inside window
                    return true;
                else if ((code0 & code1) > 0) // Bitwise AND !0, both points share an outside zone
                    return false;
                else
                {
                    // Failed both test, so calculate line segment to clip
                    // from outside point to intersection with clip edge
                    float x = 0;
                    float y = 0;
                    var codeOut = Mathf.Max(code0, code1);// Pick the one outside window

                    // Find intersection points
                    // slope = (y1 - y0) / (x1 - x0);
                    // x = x0 + (1 / slope) * (ym - y0), where ym is yMix/y_max
                    // y = y0 + slope * (xm - x0), where xm is xMin/x_max
                    if ((codeOut & TOP) > 0) // Point above clip window
                    {
                        x = from.x + (to.x - from.x) * (yMax - from.y) / (to.y - from.y);
                        y = yMax;
                    }
                    else if ((codeOut & BOTTOM) > 0) // Point below clip window
                    {
                        x = from.x + (to.x - from.x) * (yMin - from.y) / (to.y - from.y);
                        y = yMin;
                    }
                    else if ((codeOut & RIGHT) > 0) // Point is to the right of clip window
                    {
                        y = from.y + (to.y - from.y) * (xMax - from.x) / (to.x - from.x);
                        x = xMax;
                    }
                    else if ((codeOut & LEFT) > 0) // Point is to the left of clip window
                    {
                        y = from.y + (to.y - from.y) * (xMin - from.x) / (to.x - from.x);
                        x = xMin;

                    }
                    if (codeOut == code0)
                    {
                        // Now move outside point to intersection point to clip && ready for next pass
                        from.x = x;
                        from.y = y;
                        code0 = ComputeCode(from.x, from.y, xMin, yMin, xMax, yMax);
                    }
                    else
                    {
                        to.x = x;
                        to.y = y;
                        code1 = ComputeCode(to.x, to.y, xMin, yMin, xMax, yMax);
                    }
                }
            }
        }
    }
}