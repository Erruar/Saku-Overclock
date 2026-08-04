using Windows.Foundation;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Helpers;

public class TemperatureChartGenerator
{
    private readonly Queue<double> _points = new();
    private const int MaxPoints = 10;
    
    private readonly double _width;
    private readonly double _height;
    private readonly double _minTemp;
    private readonly double _maxTemp;

    public TemperatureChartGenerator(double width = 150, double height = 150, double minTemp = 0, double maxTemp = 100)
    {
        _width = width;
        _height = height;
        _minTemp = minTemp;
        _maxTemp = maxTemp;

        // Заполняем начальным состоянием (например, 40 градусов)
        for (var i = 0; i < MaxPoints; i++)
        {
            _points.Enqueue(40);
        }
    }

    public (Geometry LineGeometry, Geometry FillGeometry) AddNewPoint(double newTemp)
    {
        if (_points.Count >= MaxPoints)
        {
            _points.Dequeue();
        }

        _points.Enqueue(Math.Clamp(newTemp, _minTemp, _maxTemp));

        return BuildGeometries();
    }

    private (Geometry LineGeometry, Geometry FillGeometry) BuildGeometries()
    {
        var temps = _points.ToArray();
        List<Point> coords = new(MaxPoints);
        var stepX = _width / (MaxPoints - 1);

        // Рассчитываем физические координаты
        for (var i = 0; i < temps.Length; i++)
        {
            var x = i * stepX;
            var normalizedY = (temps[i] - _minTemp) / (_maxTemp - _minTemp);
            var y = _height - (normalizedY * _height); 
            coords.Add(new Point(x, y));
        }

        // Фигура для линии графика (незамкнутая)
        var lineFigure = new PathFigure { StartPoint = coords[0], IsClosed = false };
        // Фигура для градиентной заливки (замкнутая)
        var fillFigure = new PathFigure { StartPoint = coords[0], IsClosed = true };

        for (var i = 0; i < coords.Count - 1; i++)
        {
            var p0 = coords[Math.Max(i - 1, 0)];
            var p1 = coords[i];
            var p2 = coords[i + 1];
            var p3 = coords[Math.Min(i + 2, coords.Count - 1)];

            // Математика контрольных точек для кубической кривой Безье
            var cp1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            var cp2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);

            lineFigure.Segments.Add(new BezierSegment 
            { 
                Point1 = cp1, Point2 = cp2, Point3 = p2 
            });

            fillFigure.Segments.Add(new BezierSegment 
            { 
                Point1 = cp1, Point2 = cp2, Point3 = p2 
            });
        }

        // Для заливки уводим линию в правый нижний угол, затем в левый нижний
        fillFigure.Segments.Add(new LineSegment { Point = new Point(_width, _height) });
        fillFigure.Segments.Add(new LineSegment { Point = new Point(0, _height) });

        var lineGeometry = new PathGeometry();
        lineGeometry.Figures.Add(lineFigure);

        var fillGeometry = new PathGeometry();
        fillGeometry.Figures.Add(fillFigure);

        return (lineGeometry, fillGeometry);
    }
}