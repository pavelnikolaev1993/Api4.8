﻿using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Api4._8
{
   
    [Transaction(TransactionMode.Manual)]
    public class AddHole : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document arDoc = commandData.Application.ActiveUIDocument.Document;
            Document ovDoc = arDoc.Application.Documents.OfType<Document>().Where(x => x.Title.Contains("ОВ")).FirstOrDefault();
            if (ovDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не найден ОВ файл");
                return Result.Cancelled;
            }

            FamilySymbol familySymbol = new FilteredElementCollector(arDoc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericModel)
                .OfType<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("Отверстия"))
                .FirstOrDefault();

            if (familySymbol == null)
            {
                TaskDialog.Show("Ошибка", "Не найдено семейство \"Отверстия\"");
                return Result.Cancelled;
            }

            List<Duct> ducts = new FilteredElementCollector(ovDoc)
                .OfClass(typeof(Duct))
                .OfType<Duct>()
                .ToList();

            List<Pipe> pipes = new FilteredElementCollector(ovDoc)
                .OfClass(typeof(Pipe))
                .OfType<Pipe>()
                .ToList();

            View3D view3D = new FilteredElementCollector(arDoc)
                .OfClass(typeof(View3D))
                .OfType<View3D>()
                .Where(x => !x.IsTemplate)
                .FirstOrDefault();

            if (view3D == null)
            {
                TaskDialog.Show("Ошибка", "Не найден 3D Вид");
                return Result.Cancelled;
            }

            ReferenceIntersector referenceIntersector = new ReferenceIntersector(new ElementClassFilter(typeof(Wall)), FindReferenceTarget.Element, view3D);

            Transaction transaction = new Transaction(arDoc);
            transaction.Start("Расстановка отверстий");

            if (!familySymbol.IsActive)
            {
                familySymbol.Activate();
            }

            foreach (var d in ducts)
            {
                Line curve = (d.Location as LocationCurve).Curve as Line;
                XYZ point = curve.GetEndPoint(0);
                XYZ direction = curve.Direction;

                List<ReferenceWithContext> intersections = referenceIntersector.Find(point, direction)
                    .Where(x => x.Proximity <= curve.Length)
                    .Distinct(new ReferenceWithContextElementEqualityComparer())
                    .ToList();

                foreach (ReferenceWithContext reference in intersections)
                {
                    double proximity = reference.Proximity;
                    Reference refer = reference.GetReference();
                    Wall wall = arDoc.GetElement(refer.ElementId) as Wall;
                    Level level = arDoc.GetElement(wall.LevelId) as Level;
                    XYZ pointHole = point + (direction * proximity);

                    FamilyInstance hole = arDoc.Create.NewFamilyInstance(pointHole, familySymbol, wall, level, StructuralType.NonStructural);
                    Parameter width = hole.LookupParameter("Ширина");
                    Parameter heigth = hole.LookupParameter("Высота");

                    width.Set(d.Diameter);
                    heigth.Set(d.Diameter);
                }
            }
            foreach (var p in pipes)
            {
                Line curveP = (p.Location as LocationCurve).Curve as Line;
                XYZ pointP = curveP.GetEndPoint(0);
                XYZ directionP = curveP.Direction;

                List<ReferenceWithContext> intersectionsP = referenceIntersector.Find(pointP, directionP)
                    .Where(x => x.Proximity <= curveP.Length)
                    .Distinct(new ReferenceWithContextElementEqualityComparer())
                    .ToList();

                foreach (ReferenceWithContext reference in intersectionsP)
                {
                    double proximity = reference.Proximity;
                    Reference refer = reference.GetReference();
                    Wall wall = arDoc.GetElement(refer.ElementId) as Wall;
                    Level level = arDoc.GetElement(wall.LevelId) as Level;
                    XYZ pointHole = pointP + (directionP * proximity);

                    FamilyInstance hole = arDoc.Create.NewFamilyInstance(pointHole, familySymbol, wall, level, StructuralType.NonStructural);
                    Parameter width = hole.LookupParameter("Ширина");
                    Parameter heigth = hole.LookupParameter("Высота");

                    width.Set(p.Diameter);
                    heigth.Set(p.Diameter);
                }
            }

            transaction.Commit();

            return Result.Succeeded;
        }
    }

    public class ReferenceWithContextElementEqualityComparer : IEqualityComparer<ReferenceWithContext>
    {
        public bool Equals(ReferenceWithContext x, ReferenceWithContext y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(null, x)) return false;
            if (ReferenceEquals(null, y)) return false;

            var xReference = x.GetReference();

            var yReference = y.GetReference();

            return xReference.LinkedElementId == yReference.LinkedElementId
                       && xReference.ElementId == yReference.ElementId;
        }

        public int GetHashCode(ReferenceWithContext obj)
        {
            var reference = obj.GetReference();

            unchecked
            {
                return (reference.LinkedElementId.GetHashCode() * 397) ^ reference.ElementId.GetHashCode();
            }
        }
    }
}

