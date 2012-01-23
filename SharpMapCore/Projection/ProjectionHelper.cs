﻿using System;
using System.Collections.Generic;
using ProjNet.CoordinateSystems.Transformations;
using SharpMap.Geometries;

namespace SharpMap.Projection
{
    public static class ProjectionHelper
    {
        public static void Transform(IGeometry geometry, ICoordinateTransformation CoordinateTransformation)
        {
            var vertices = AllVertices(geometry);
            foreach (var vertex in vertices)
            {
                double[] point = CoordinateTransformation.MathTransform.Transform(new double[] { vertex.X, vertex.Y });
                vertex.X = point[0];
                vertex.Y = point[1];
            }
        }

        public static void InverseTransform(IGeometry geometry, ICoordinateTransformation CoordinateTransformation)
        {
            var vertices = AllVertices(geometry);
            foreach (var vertex in vertices)
            {
                CoordinateTransformation.MathTransform.Inverse();
                double[] point = CoordinateTransformation.MathTransform.Transform(new double[] { vertex.X, vertex.Y });
                CoordinateTransformation.MathTransform.Inverse();
                vertex.X = point[0];
                vertex.Y = point[1];
            }
        }

        public static BoundingBox Transform(BoundingBox box, ICoordinateTransformation CoordinateTransformation)
        {
            double[] point1 = CoordinateTransformation.MathTransform.Transform(new double[] { box.MinX, box.MinY });
            double[] point2 = CoordinateTransformation.MathTransform.Transform(new double[] { box.MaxX, box.MaxY });
            return new BoundingBox(point1[0], point1[1], point2[0], point2[1]);
        }

        public static BoundingBox InverseTransform(BoundingBox box, ICoordinateTransformation CoordinateTransformation)
        {
            CoordinateTransformation.MathTransform.Invert();
            double[] point1 = CoordinateTransformation.MathTransform.Transform(new double[] { box.MinX, box.MinY });
            double[] point2 = CoordinateTransformation.MathTransform.Transform(new double[] { box.MaxX, box.MaxY });
            CoordinateTransformation.MathTransform.Invert();
            return new BoundingBox(point1[0], point1[1], point2[0], point2[1]);
        }

        public static IEnumerable<Point> AllVertices(IGeometry geometry)
        {
            if (geometry == null)
                return new Point[0];
            if (geometry is Point)
                return new[] { (Point)geometry };
            if (geometry is LineString)
                return AllVertices((LineString)geometry);
            if (geometry is Polygon)
                return AllVertices((Polygon)geometry);
            if (geometry is IEnumerable<Geometry>)
                return AllVertices((IEnumerable<Geometry>)geometry);
            var format = String.Format("unsupported geometry: {0}", geometry.GetType().Name);
            throw new NotSupportedException(format);
        }

        private static IEnumerable<Point> AllVertices(LineString lineString)
        {
            if (lineString == null)
                throw new ArgumentNullException("lineString");
            return lineString.Vertices;
        }

        private static IEnumerable<Point> AllVertices(Polygon polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException("polygon");

            foreach (var point in polygon.ExteriorRing.Vertices)
                yield return point;
            foreach (var ring in polygon.InteriorRings)
                foreach (var point in ring.Vertices)
                    yield return point;
        }

        private static IEnumerable<Point> AllVertices(IEnumerable<Geometry> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");

            foreach (var geometry in collection)
                foreach (var point in AllVertices(geometry))
                    yield return point;
        }
    }
}
