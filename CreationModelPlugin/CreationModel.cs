using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;

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
            AddRoof(doc, level2, walls, width, depth);

            transaction.Commit();

            return Result.Succeeded;
        }

        private void AddRoof(Document doc, Level level2, List<Wall> walls, double width, double depth)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Generic - 400mm"))
                .Where(x => x.FamilyName.Equals("Basic Roof"))
                .FirstOrDefault();

            View view = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .OfType<View>()
                .Where(x => x.Name.Equals("Level 1"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;
            double extrusionStart = -width / 2 - dt;
            double extrusionEnd = width / 2 + dt;
            double curveStart = -depth / 2 - dt;
            double curveEnd = depth / 2 + dt;

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level2.Elevation), new XYZ(0, 0, level2.Elevation + 10)));
            curveArray.Append(Line.CreateBound(new XYZ(0,0, level2.Elevation+10), new XYZ(0, curveEnd, level2.Elevation)));

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), view);
            ExtrusionRoof extrusionRoof=doc.Create.NewExtrusionRoof(curveArray,plane, level2, roofType, extrusionStart, extrusionEnd);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;

            //List<XYZ> points = new List<XYZ>();
            //points.Add(new XYZ(-dt, -dt, 0));
            //points.Add(new XYZ(dt, -dt, 0));
            //points.Add(new XYZ(dt, dt, 0));
            //points.Add(new XYZ(-dt, dt, 0));
            //points.Add(new XYZ(-dt, -dt, 0));

            //Application application = doc.Application;
            //CurveArray footprint = application.Create.NewCurveArray();
            //for (int i = 0; i < 4; i++)
            //{
            //    LocationCurve curve = walls[i].Location as LocationCurve;
            //    XYZ p1 = curve.Curve.GetEndPoint(0);
            //    XYZ p2 = curve.Curve.GetEndPoint(1);
            //    Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
            //    footprint.Append(line);
            //}
            //ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
            //FootPrintRoof footprintroof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);
            //foreach (ModelCurve m in footPrintToModelCurveMapping)
            //{
            //    footprintroof.set_DefinesSlope(m, true);
            //    footprintroof.set_SlopeAngle(m, 0.5);
            //}
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
