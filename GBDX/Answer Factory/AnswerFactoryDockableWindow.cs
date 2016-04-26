﻿// Author: Russ Wittmer

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;

namespace Gbdx.Answer_Factory
{
    using System.Linq;
    using System.Net;
    using System.Windows.Controls;
    using System.Windows.Forms;

    using AnswerFactory;

    using GbdxSettings;
    using GbdxSettings.Properties;

    using System.Windows.Threading;

    using ESRI.ArcGIS.Geometry;

    using Gbdx.Vector_Index;

    using GbdxTools;

    using NetworkConnections;

    using Newtonsoft.Json;

    using RestSharp;

    using ListViewItem = System.Windows.Forms.ListViewItem;
    using UserControl = System.Windows.Forms.UserControl;

    /// <summary>
    /// Designer class of the dockable window add-in. It contains user interfaces that
    /// make up the dockable window.
    /// </summary>
    public partial class AnswerFactoryDockableWindow : UserControl
    {
        private string token;

        List<Recipe> recipeList = new List<Recipe>();

        List<Project2> existingProjects = new List<Project2>();

        private IRestClient client;

        public AnswerFactoryDockableWindow(object hook)
        {
            this.InitializeComponent();
            this.client = new RestClient(Settings.Default.baseUrl);
            this.GetToken();
            this.Hook = hook;
        }

        /// <summary>
        /// Get Token to use with GBDX services
        /// </summary>
        /// <returns></returns>
        private void GetToken()
        {
            IRestClient restClient = new RestClient(Settings.Default.AuthBase);

            string password;
            var result = Encryption.Aes.Instance.Decrypt128(Settings.Default.password, out password);

            if (!result)
            {
                Jarvis.Logger.Warning("PASSWORD FAILED DECRYPTION");
                return;
            }

            var request = new RestRequest(Settings.Default.authenticationServer, Method.POST);
            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddHeader("Authorization", string.Format("Basic {0}", Settings.Default.apiKey));
            request.AddParameter("grant_type", "password");
            request.AddParameter("username", Settings.Default.username);
            request.AddParameter("password", password);

            //var response = restClient.Execute<AccessToken>(request);
            restClient.ExecuteAsync<AccessToken>(
                request,
                resp =>
                    {
                        Jarvis.Logger.Info(resp.ResponseUri.ToString());

                        if (resp.StatusCode == HttpStatusCode.OK && resp.Data != null)
                        {
                            this.token = resp.Data.access_token;
                            this.GetRecipes();
                            this.GetExistingProjects();
                        }
                        else
                        {
                            this.token = string.Empty;
                        }
                    });
        }

        private void GetRecipes()
        {
            this.CheckBaseUrl();
            var request = new RestRequest("/answer-factory-recipe-service/api/recipes", Method.GET);
            request.AddHeader("Authorization", "Bearer" + this.token);
            request.AddHeader("Content-Type", "application/json");

            this.client.ExecuteAsync<List<Recipe>>(
                request,
                resp =>
                    {
                        Jarvis.Logger.Info(resp.ResponseUri.ToString());
                        if (resp.StatusCode == HttpStatusCode.OK && resp.Data != null)
                        {
                            this.recipeList = resp.Data;
                            this.Invoke(
                                (MethodInvoker)(() =>
                                    {
                                        foreach (var item in this.recipeList)
                                        {
                                            this.availableRecipesCombobox.Items.Add(item.name);
                                        }
                                    }));
                        }
                    });
        }

        private void CheckBaseUrl()
        {
            if (this.client == null || !this.client.BaseUrl.Equals(new Uri(Settings.Default.baseUrl)))
            {
                this.client = new RestClient(Settings.Default.baseUrl);
            }
        }

        private string CreateProjectJson(List<IPolygon> polygons)
        {
            // get the geojson of the aois
            var aoi = Jarvis.ConvertPolygonsToGeoJson(polygons);
            var newProject = new Project();
            newProject.aois.Add(aoi);
            newProject.name = this.projectNameTextbox.Text;

            if (this.availableRecipesCombobox.SelectedIndex != -1)
            {
                var recName =
                    this.availableRecipesCombobox.Items[this.availableRecipesCombobox.SelectedIndex].ToString();
                var recipe = this.GetRecipe(recName);

                if (recipe != null)
                {
                    var recipeConfig = new RecipeConfig { recipeId = recipe.id, recipeName = recipe.name };
                    newProject.recipeConfigs.Add(recipeConfig);
                }
            }

            var projectJson = JsonConvert.SerializeObject(newProject).Replace("\\", "");

            projectJson = projectJson.Replace("\"aois\":[\"{\"", "\"aois\":[{\"");
            projectJson = projectJson.Replace("\"],\"recipeConfigs\"", "],\"recipeConfigs\"");
            return projectJson;
        }

        private Recipe GetRecipe(string name)
        {
            IEnumerable<Recipe> query = (from q in this.recipeList where q.name.Equals(name) select q);

            if (query.Count() == 1)
            {
                return query.Single();
            }
            return null;
        }

        private void GetExistingProjects()
        {
            var request = new RestRequest("/answer-factory-project-service/api/project", Method.GET);
            request.AddHeader("Authorization", "Bearer " + this.token);
            request.AddHeader("Content-Type", "application/json");

            this.CheckBaseUrl();
            this.client.ExecuteAsync<List<Project2>>(
                request,
                resp =>
                    {
                        Jarvis.Logger.Info(resp.ResponseUri.ToString());

                        if (resp.Data != null && resp.StatusCode == HttpStatusCode.OK)
                        {
                            this.existingProjects = resp.Data;
                            this.Invoke((MethodInvoker)(() => { this.UpdateUiWithExistingProjects(); }));
                        }
                    });
        }

        private void GetResult(string id, string recipeName)
        {
            this.CheckBaseUrl();

            if (string.IsNullOrEmpty(id))
            {
                MessageBox.Show("No Project id");
                return;
            }
            var request = new RestRequest(
                string.Format("/answer-factory-recipe-service/api/result/project/{0}", id),
                Method.GET);

            request.AddHeader("Authorization", "Bearer " + this.token);
            request.AddHeader("Content-Type", "application/json");
            this.client.ExecuteAsync<List<ResultItem>>(
                request,
                resp =>
                    {
                        ProcessResult(recipeName, resp, this.existingProjects, this.token, this.client);
                    });
        }

        private static void ProcessResult(
            string recipeName,
            IRestResponse<List<ResultItem>> resp,
            List<Project2> existingProjects,
            string token,
            IRestClient client)
        {
            Jarvis.Logger.Info(resp.ResponseUri.ToString());

            if (resp.Data != null)
            {
                var query = from a in resp.Data where a.recipeName == recipeName select a;


                foreach (var item in query)
                {
                    var aoiQuery = from b in existingProjects where b.id == item.projectId select b;
                    string aoi = string.Empty;
                    foreach (var projectItem in aoiQuery)
                    {
                        aoi = ConvertAoisToGeometryCollection(projectItem.aois);
                    }

                    Jarvis.Logger.Info(item.properties.query_string);
                    GetGeometries(item.properties.query_string, token, aoi, client);
                }
            }
        }

        private void UpdateUiWithExistingProjects()
        {
            this.existingProjectsListView.Items.Clear();
            foreach (var item in this.existingProjects)
            {
                foreach (var recipe in item.recipeConfigs)
                {
                    string[] arr = new string[4];
                    arr[0] = item.name;
                    arr[1] = recipe.recipeName;
                    arr[2] = "Uknown";
                    arr[3] = item.id;
                    this.existingProjectsListView.Items.Add(new ListViewItem(arr));
                }
            }
        }

        private void GetProjectRecipeStatus(string projectid)
        {
            this.CheckBaseUrl();

            var request =
                new RestRequest(string.Format("/answer-factory-recipe-service/api/result/project/{0}", projectid));
            request.AddHeader("Authorization", "Bearer " + this.token);
            request.AddHeader("Content-Type", "application/json");

            var response = this.client.ExecuteAsync(
                request,
                resp => { Jarvis.Logger.Info(resp.ResponseUri.ToString()); });
        }

        private static string ConvertAoisToGeometryCollection(List<string> aois)
        {
            StringBuilder builder = new StringBuilder("{\"type\":\"GeometryCollection\",\"geometries\":[");
            foreach (var item in aois)
            {
                builder.Append(item + ",");
            }
            builder.Remove(builder.Length - 1, 1);
            builder.Append("]}");
            return builder.ToString();
        }


        #region Download Vector Items

        private static void GetGeometries(string query, string token, string aoi, IRestClient client, int attempt = 0)
        {
            var request = new RestRequest("/insight-vector/api/shape/query/geometries?q=" + query, Method.POST);
            request.AddHeader("Authorization", "Bearer " + token);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", aoi, ParameterType.RequestBody);

            attempt++;

            client.ExecuteAsync<ResponseData>(
                request,
                resp => GetGeometriesResponseProcess(resp, query, token, aoi, client, attempt));
        }

        private static void GetGeometriesResponseProcess(
            IRestResponse<ResponseData> resp,
            string query,
            string token,
            string aoi,
            IRestClient client,
            int geomAttempts)
        {
            Jarvis.Logger.Info(resp.ResponseUri.ToString());

            // Check to see if we have a good status if not try it again
            if (resp.StatusCode != HttpStatusCode.OK && geomAttempts <= 3)
            {
                GetGeometries(query, token, aoi, client, geomAttempts);
            }
            else if (geomAttempts > 3)
            {
                MessageBox.Show("An Error occurred.  Please try again");
                return;
            }

            if (resp.Data != null)
            {
                var sources = resp.Data;

                foreach (var source in sources.data)
                {
                    GetTypes(source.name, query, token, aoi, client);
                }
            }

        }

        private static void GetTypes(
            string geometry,
            string query,
            string token,
            string aoi,
            IRestClient client,
            int attempts = 0)
        {
            var request =
                new RestRequest(
                    string.Format("/insight-vector/api/shape/query/{0}/types?q={1}", geometry, query),
                    Method.POST);
            request.AddHeader("Authorization", "Bearer " + token);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", aoi, ParameterType.RequestBody);

            attempts++;

            client.ExecuteAsync<ResponseData>(
                request,
                resp => GetTypesResponseProcess(resp, geometry, query, token, aoi, client, attempts));

        }

        private static void GetTypesResponseProcess(
            IRestResponse<ResponseData> resp,
            string geometry,
            string query,
            string token,
            string aoi,
            IRestClient client,
            int typeAttempts)
        {
            Jarvis.Logger.Info(resp.ResponseUri.ToString());

            // Check to see if we have a good status if not try it again
            if (resp.StatusCode != HttpStatusCode.OK && typeAttempts <= 3)
            {
                GetTypes(geometry, query, token, aoi, client, typeAttempts);
            }
            else if (typeAttempts > 3)
            {
                MessageBox.Show("An Error occurred.  Please try again");
                return;
            }

            if (resp.Data != null)
            {
                var types = resp.Data;
                foreach (var type in types.data)
                {
                    GetPagingId(geometry, type.name, query, token, aoi, client);
                }
            }


        }

        private static void GetPagingId(
            string geometry,
            string type,
            string query,
            string token,
            string aoi,
            IRestClient client,
            int attempts = 0)
        {
            var request =
                new RestRequest(
                    string.Format("/insight-vector/api/shape/query/{0}/{1}/paging?q={2}", geometry, type, query),
                    Method.POST);
            request.AddHeader("Authorization", "Bearer " + token);
            request.AddHeader("Content-Type", "application/json");
            request.AddParameter("application/json", aoi, ParameterType.RequestBody);

            attempts ++;

            client.ExecuteAsync<PageId>(
                request,
                resp => GetPageIdResponseProcess(resp, geometry, type, query, token, aoi, client, attempts));

        }

        private static void GetPageIdResponseProcess(
            IRestResponse<PageId> resp,
            string geometry,
            string type,
            string query,
            string token,
            string aoi,
            IRestClient client,
            int pageIdAttempts = 0)
        {
            Jarvis.Logger.Info(resp.ResponseUri.ToString());

            // Check to see if we have a good status if not try it again
            if (resp.StatusCode != HttpStatusCode.OK && pageIdAttempts <= 3)
            {
                GetPagingId(geometry, type, query, token, aoi, client, pageIdAttempts);
            }
            else if (pageIdAttempts > 3)
            {
                MessageBox.Show("An Error occurred.  Please try again");
                return;
            }
            if (resp.Data != null)
            {
                var pageId = resp.Data;
            }
        }

        private static void GetPages(string pageId, IRestClient client)
        {

        }

        #endregion

        /// <summary>
        /// Host object of the dockable window
        /// </summary>
        private object Hook { get; set; }

        /// <summary>
        /// Implementation class of the dockable window add-in. It is responsible for 
        /// creating and disposing the user interface class of the dockable window.
        /// </summary>
        public class AddinImpl : ESRI.ArcGIS.Desktop.AddIns.DockableWindow
        {
            private AnswerFactoryDockableWindow m_windowUI;

            public AddinImpl()
            {
            }

            protected override IntPtr OnCreateChild()
            {
                m_windowUI = new AnswerFactoryDockableWindow(this.Hook);
                return m_windowUI.Handle;
            }

            protected override void Dispose(bool disposing)
            {
                if (m_windowUI != null)
                {
                    m_windowUI.Dispose(disposing);
                }

                base.Dispose(disposing);
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            try
            {
                // check for a project name
                if (string.IsNullOrEmpty(this.projectNameTextbox.Text))
                {
                    MessageBox.Show("Project Name Required.");
                    return;
                }

                var polygons = Jarvis.GetPolygons(ArcMap.Document.FocusMap);

                // check to make sure an aoi(s) have been selected.
                if (polygons.Count == 0)
                {
                    MessageBox.Show("Please select polygon(s)");
                    return;
                }

                var projectJson = this.CreateProjectJson(polygons);

                var request = new RestRequest("/answer-factory-project-service/api/project", Method.POST);
                request.AddHeader("Authorization", "Bearer " + this.token);
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", projectJson, ParameterType.RequestBody);
                this.CheckBaseUrl();
                this.client.ExecuteAsync(request, resp => { Jarvis.Logger.Info(resp.ResponseUri.ToString()); });

                this.projectNameTextbox.Clear();
                this.availableRecipesCombobox.SelectedIndex = -1;
            }
            catch (Exception error)
            {
                Jarvis.Logger.Error(error);
            }
        }

        private void showResultsButton_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in this.existingProjectsListView.SelectedItems)
            {
                var name = item.SubItems[0].Text;
                var recipeName = item.SubItems[1].Text;
                var id = item.SubItems[3].Text;

                // linq query to get the project where the all the criteria match just as a double check
                var query = from proj in this.existingProjects
                            from recipe in proj.recipeConfigs
                            where proj.id == id
                            where proj.name == name
                            where recipe.recipeName == recipeName
                            select proj;

                var selectedItems = query as Project2[] ?? query.ToArray();
                if (selectedItems.Any())
                {
                    this.GetResult(id, recipeName);
                }
                else
                {
                    MessageBox.Show("Error in selection.  Please try again.");
                }
                return;
            }
        }

        private void resultRefrshButton_Click(object sender, EventArgs e)
        {
            this.GetExistingProjects();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            this.projectNameTextbox.Clear();
            this.bufferTextbox.Clear();
            this.availableRecipesCombobox.SelectedIndex = -1;
        }
    }
}