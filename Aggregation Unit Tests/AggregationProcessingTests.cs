﻿using System;
using System.Text;
using System.Collections.Generic;
using Aggregations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RestSharp;
using RestSharp.Deserializers;

namespace Aggregation_Unit_Tests
{
    using System.Linq;

    using ESRI.ArcGIS;
    using ESRI.ArcGIS.esriSystem;
    using ESRI.ArcGIS.Geometry;
    using ESRI.ArcGIS.Geodatabase;

    using GbdxTools;

    /// <summary>
    /// Summary description for AggregationProcessingTests
    /// </summary>
    [TestClass]
    public class AggregationProcessingTests
    {
        private JsonDeserializer deserial = new JsonDeserializer();

        public AggregationProcessingTests()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        /// <summary>
        /// The init test.
        /// </summary>
        [TestInitialize()]
        public void InitTest()
        {
            RuntimeManager.Bind(ProductCode.Desktop);
            AoInitialize aoInit = new AoInitializeClass();
            aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeAdvanced);
        }

        [TestMethod]
        public void TestProcessAggregations()
        {
            AggregationHelper testTarget = new AggregationHelper();

            var mog = deserial.Deserialize<MotherOfGodAggregations>(new RestResponse<MotherOfGodAggregations>
            {
                Content = TestResources.MogJson
            });
            }

        
        [TestMethod]
        public void TestSingleValueAggregation()
        {
            var mog = this.deserial.Deserialize<MotherOfGodAggregations>(
                    new RestResponse<MotherOfGodAggregations> { Content = TestResources.sentimentAggregation });
            var output = new Dictionary<string, Dictionary<string, double>>();
            var fieldNames = new Dictionary<string, string>();
            AggregationHelper.ProcessAggregations(mog.aggregations,0,ref output,string.Empty,false,ref fieldNames);

        }

        [TestMethod]
        public void TestNestedAggregations()
        {
            var mog =
                this.deserial.Deserialize<MotherOfGodAggregations>(
                    new RestResponse<MotherOfGodAggregations> { Content = TestResources.doubleAggregation });
            var output = new Dictionary<string, Dictionary<string, double>>();
            var fieldNames = new Dictionary<string,string>();
            AggregationHelper.ProcessAggregations(mog.aggregations,0,ref output, string.Empty, false, ref fieldNames);

            bool success = false;
            foreach (var item in output.Keys)
            {
                if (output[item].Keys.Any(subItem => subItem == "HGIS"))
                {
                    success = true;
                }

                if (success)
                {
                    break;
                }
            }

            Assert.IsTrue(success);
        }

        [TestMethod]
        public void TestJsonDeserializer()
        {
            var jsonGeometryPoint = TestResources.testData;

            var jsonReader = new JSONReaderClass();
            jsonReader.ReadFromString(jsonGeometryPoint);
            var jsonDeserializer = new JSONDeserializerGdbClass();
            jsonDeserializer.InitDeserializer(jsonReader, null);
            IGeometry geometry = ((IExternalDeserializerGdb)jsonDeserializer).ReadGeometry(esriGeometryType.esriGeometryPolygon);
            IPolygon point = (IPolygon)geometry;

            Assert.IsTrue(true);
        }


        [TestMethod]
        public void TestGeoJsonToEsriGeometryPolygon()
        {
            var polyJson = TestResources.NorthCarolinaPolygonGeoJson;


            PrivateType pt = new PrivateType(typeof(GeometryClasses));

            var poly = (IPolygon)pt.InvokeStatic("GeoJsonToEsriPolygon", polyJson);

            if (poly != null)
            {
                Assert.IsTrue(true);
            }
        }


        [TestMethod]
        public void TestPolygoGeoJsonObjectToEsriJson()
        {
            var polyJson = TestResources.NorthCarolinaPolygonGeoJson;
            var deserializedObject =
                this.deserial.Deserialize<PolygonGeoJson>(
                    new RestResponse<PolygonGeoJson> { Content = polyJson });
            PrivateType pt = new PrivateType(typeof(GeometryClasses));
            var output = (string) pt.InvokeStatic("GeojsonToEsriJson", deserializedObject);
            Assert.IsTrue(output.Equals(TestResources.NorthCarolinaPolygonEsriJson));
        }

        [TestMethod]
        public void TestGeoJsonMultiPolygonDeserialization()
        {
            var multiPolyJson = TestResources.CoNcMultiPolygonGeoJson;
            var deserializedObject =
                this.deserial.Deserialize<MultiPolygonGeoJson>(
                    new RestResponse<MultiPolygonGeoJson> { Content = multiPolyJson });

            Assert.IsTrue(deserializedObject.coordinates.Count == 2);
        }

        [TestMethod]
        public void TestPolygonGeoJsonObjectToEsriJsonList()
        {
            var multiPolyJson = TestResources.CoNcMultiPolygonGeoJson;
            var deserializedObject =
                this.deserial.Deserialize<MultiPolygonGeoJson>(
                    new RestResponse<MultiPolygonGeoJson> { Content = multiPolyJson });
            var test = new GeometryClasses();
            PrivateType privateHelperObject = new PrivateType(typeof(GeometryClasses));
            var output = (List<string>)privateHelperObject.InvokeStatic("MultiPolygonObjectToEsriJson", new object[]{ deserializedObject});
//            var output = GeometryClasses.MultiPolygonObjectToEsriJson(deserializedObject);

            Assert.IsTrue(output.Count == 2);
        }

        [TestMethod]
        public void TestDeserializeGeoJsonBaseObject()
        {
            var multiPolyJson = TestResources.CoNcMultiPolygonGeoJson;
            var polygonJson = TestResources.NorthCarolinaPolygonGeoJson;

            var deserializedMultiPoly =
                this.deserial.Deserialize<GeoJsonBaseObject>(
                    new RestResponse<MultiPolygonGeoJson> { Content = multiPolyJson });
            var deserializedPoly =
                this.deserial.Deserialize<GeoJsonBaseObject>(
                    new RestResponse<MultiPolygonGeoJson> { Content = polygonJson });

            Assert.IsTrue(deserializedPoly.type.Equals("Polygon") && deserializedMultiPoly.type.Equals("MultiPolygon"));
        }


        [TestMethod]
        public void TestGeoJsonBaseObjectMultiPolygonToEsriJson()
        {
            var multiPolyJson = TestResources.CoNcMultiPolygonGeoJson;

            var output = GeometryClasses.GeoJsonObjectToEsriJsonList(multiPolyJson);

            Assert.IsTrue(output.Count == 2);
        }

        [TestMethod]
        public void TestGeoJsonSingleToEsriJson()
        {
            var poly = TestResources.NorthCarolinaPolygonGeoJson;
            var output = GeometryClasses.GeoJsonObjectToEsriJsonList(poly);

            Assert.IsTrue(output.Count == 1);
        }

        [TestMethod]
        public void MultiPolygonGeoJsonToEsriPolygonList()
        {
            var multiPolyJson = TestResources.CoNcMultiPolygonGeoJson;
            var output = GeometryClasses.AoiGeoJsonToEsriPolygons(multiPolyJson);

            Assert.IsTrue(output.Count == 2);
        }
    }
}
