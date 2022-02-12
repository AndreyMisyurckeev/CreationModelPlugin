using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CreationModelPlugin
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> listLevel = new FilteredElementCollector(doc)
                 .OfClass(typeof(Level))
                 .OfType<Level>()
                 .ToList();

            Level level1 = listLevel
                .Where(x => x.Name.Equals("Level 1"))
                .FirstOrDefault();

            Level level2 = listLevel
                .Where(x => x.Name.Equals("Level 2"))
                .FirstOrDefault();

            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();

            List<Wall> walls = CreateFourWalls(doc, width, depth, level1, level2);
            AddDoor(doc, level1, walls[0]);
            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

            transaction.Commit();

            return Result.Succeeded;
        }
        public List<Wall> CreateFourWalls(Document doc, double width, double depth, Level down, Level up)
        {
            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();
            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, down.Id, false);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(up.Id);
                walls.Add(wall);
            }
            return walls;
        }
        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name == "0762 x 2032mm")
                .Where(x => x.FamilyName == "M_Single-Flush")
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!doorType.IsActive)
            {
                doorType.Activate();
            }

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);
        }


        private void AddWindow(Document doc, Level level1, Wall wall)
        {
            FamilySymbol pasteWindow = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name == "0915 x 1830mm")
                .Where(x => x.FamilyName == "M_Fixed")
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            if (!pasteWindow.IsActive)
            {
                pasteWindow.Activate();
            }

            FamilyInstance window =
            doc.Create.NewFamilyInstance(point, pasteWindow, wall, level1, StructuralType.NonStructural);
            double height = UnitUtils.ConvertToInternalUnits(800, UnitTypeId.Millimeters);
            window.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM).Set(height);
            window.flipFacing();
        }
    }
}
