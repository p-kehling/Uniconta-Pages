using UnicontaClient.Models;
using DevExpress.DashboardCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;
using Uniconta.API.System;
using Uniconta.ClientTools;
using Uniconta.ClientTools.Controls;
using Uniconta.ClientTools.DataModel;
using Uniconta.ClientTools.Page;
using Uniconta.Common;
using Uniconta.DataModel;
using UnicontaClient.Controls;
using UnicontaClient.Utilities;
using Uniconta.API.Service;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.PivotGrid;
using Uniconta.Common.Utility;
using System.Reflection.Emit;
using Uniconta.Reports.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Data;
using System.Collections;
using System.Web.Caching;
using DevExpress.XtraPrinting.Native;
using Uniconta.ClientTools.Util;
using DevExpress.XtraSpreadsheet.Model;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public partial class DashBoardViewerPage : ControlBasePage
    {
        public override string NameOfControl { get { return TabControls.DashBoardViewerPage; } }
        Type tableType;
        DashboardClient _selectedDashBoard;
        Dictionary<string, Type> dataSourceAndTypeMap;
        CWServerFilter filterDialog;
        public FilterSorter PropSort;
        private Dictionary<string, List<FilterProperties>> lstOfNewFilters;
        private Dictionary<string, FilterSorter> lstOfSorters;
        private Dictionary<int, Type[]> ListOfReportTableTypes;
        public IEnumerable<PropValuePair> filterValues;
        Dictionary<string, List<DashboardUserField>> dashboardUserFields;
        string selectedDataSourceName;
        Company company;
        DashboardState dState;
        bool LoadOnOpen;
        List<FixedCompany> fixedCompanies;
        string dashboardName;
        System.Windows.Forms.Timer timer;
        string UserIdPropName;
        UnicontaBaseEntity master;
        string masterField;
        string titleText = string.Empty;
        public DashBoardViewerPage(SynchronizeEntity syncEntity) : base(true, syncEntity)
        {
            master = syncEntity.Row;
            this.InSyncMode = true;
            InitPage();
        }
        public DashBoardViewerPage(UnicontaBaseEntity entity) : base(entity)
        {
            _selectedDashBoard = entity as DashboardClient;
            if (_selectedDashBoard == null)
                master = entity;
            InitPage();
        }

        public DashBoardViewerPage(BaseAPI API)
            : base(API, string.Empty)
        {
            InitPage();
        }

        protected override void SyncEntityMasterRowChanged(UnicontaBaseEntity args)
        {
            master = args;
            SetHeader(master);
            dashboardViewerUniconta?.ReloadData();
        }

        void SetHeader(UnicontaBaseEntity row)
        {
            var keystr = (row as IdKeyName)?.KeyStr;
            string header = string.Empty;
            if (string.IsNullOrEmpty(keystr))
                header = string.Format("{0}: {1}", Uniconta.ClientTools.Localization.lookup("Dashboard"), dashboardName);
            else
                header = string.Format("{0}:{1}/{2} ", Uniconta.ClientTools.Localization.lookup("Dashboard"), dashboardName, keystr);
            SetHeader(header);
        }
        void InitPage()
        {
            dState = new DashboardState();
            company = api.CompanyEntity;
            InitializeComponent();
            this.BusyIndicator = busyIndicator;
            dashboardViewerUniconta.ObjectDataSourceLoadingBehavior = DevExpress.DataAccess.DocumentLoadingBehavior.LoadAsIs;
            dataSourceAndTypeMap = new Dictionary<string, Type>();
            lstOfNewFilters = new Dictionary<string, List<FilterProperties>>();
            lstOfSorters = new Dictionary<string, FilterSorter>();
            ListOfReportTableTypes = new Dictionary<int, Type[]>();
            LoadListOfTableTypes(company);
            dashboardViewerUniconta.Loaded += DashboardViewerUniconta_Loaded;
            dashboardViewerUniconta.DashboardLoaded += DashboardViewerUniconta_DashboardLoaded;
            dashboardViewerUniconta.SetInitialDashboardState += DashboardViewerUniconta_SetInitialDashboardState;
            dashboardViewerUniconta.DashboardChanged += DashboardViewerUniconta_DashboardChanged;
            dashboardViewerUniconta.DataLoadingError += DashboardViewerUniconta_DataLoadingError;
            dashboardViewerUniconta.Unloaded += DashboardViewerUniconta_Unloaded;
        }

        private void DashboardViewerUniconta_DataLoadingError(object sender, DataLoadingErrorEventArgs e)
        {
            e.Handled = true;
        }

        public override void PageClosing()
        {
            if (timer != null)
                timer.Tick -= Timer_Tick;
            base.PageClosing();
        }

        public override void SetParameter(IEnumerable<ValuePair> Parameters)
        {
            foreach (var rec in Parameters)
            {
                if (rec.Name == null || string.Compare(rec.Name, "Dashboard", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    dashboardName = rec.Value;
                    SetHeader(string.Concat(Uniconta.ClientTools.Localization.lookup("Dashboard"), ": ", dashboardName));
                }
                if (string.Compare(rec.Name, "Field", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    masterField = rec.Value;
                }
            }
            base.SetParameter(Parameters);
        }

        private void DashboardViewerUniconta_SetInitialDashboardState(object sender, DevExpress.DashboardWpf.SetInitialDashboardStateWpfEventArgs e)
        {
            e.InitialState = dState;
        }

        void SetLabelValues(PivotDashboardItem pivot, List<DataItem> targetLst)
        {
            foreach (var col in targetLst)
            {
                var name = ((DataItem)col).Name;
                var savedName = pivot.CustomProperties.GetValue(col.UniqueId) ?? string.Empty;

                if ((name != null || !string.IsNullOrEmpty(name)) && (col.Name.StartsWith("&") || col.Name.StartsWith("@") || savedName.StartsWith("@") || savedName.StartsWith("&")))
                {
                    name = savedName;
                    ((DataItem)col).Name = Uniconta.ClientTools.Localization.lookup(name.Substring(1));
                }
            }
        }

        void SetChartLabelValues(ChartDashboardItem chart, List<DataItem> targetLst)
        {
            foreach (var col in targetLst)
            {
                var name = ((DataItem)col).Name;
                var savedName = chart.CustomProperties.GetValue(col.UniqueId) ?? string.Empty;

                if ((name != null || !string.IsNullOrEmpty(name)) && (col.Name.StartsWith("&") || col.Name.StartsWith("@") || savedName.StartsWith("@") || savedName.StartsWith("&")))
                {
                    name = savedName;
                    ((DataItem)col).Name = Uniconta.ClientTools.Localization.lookup(name.Substring(1));
                }
            }
        }

        void SetPieLabelValues(PieDashboardItem pie, List<DataItem> targetLst)
        {
            foreach (var col in targetLst)
            {
                var name = ((DataItem)col).Name;
                var savedName = pie.CustomProperties.GetValue(col.UniqueId) ?? string.Empty;

                if ((name != null || !string.IsNullOrEmpty(name)) && (col.Name.StartsWith("&") || col.Name.StartsWith("@") || savedName.StartsWith("@") || savedName.StartsWith("&")))
                {
                    name = savedName;
                    ((DataItem)col).Name = Uniconta.ClientTools.Localization.lookup(name.Substring(1));
                }
            }
        }

        void SetScatterChartLabelValues(ScatterChartDashboardItem sctChart, List<DataItem> targetLst)
        {
            foreach (var col in targetLst)
            {
                var name = ((DataItem)col).Name;
                var savedName = sctChart.CustomProperties.GetValue(col.UniqueId) ?? string.Empty;

                if ((name != null || !string.IsNullOrEmpty(name)) && (col.Name.StartsWith("&") || col.Name.StartsWith("@") || savedName.StartsWith("@") || savedName.StartsWith("&")))
                {
                    name = savedName;
                    ((DataItem)col).Name = Uniconta.ClientTools.Localization.lookup(name.Substring(1));
                }
            }
        }

        private void DashboardViewerUniconta_DashboardLoaded(object sender, DevExpress.DashboardWpf.DashboardLoadedEventArgs e)
        {
            Dashboard dasdboard = e.Dashboard;
            foreach (var item in dasdboard?.Items)
            {
                var name = dasdboard?.CustomProperties.GetValue(item.ComponentName);
                if (name != null)
                {
                    if (name[0] == '&' || name[0] == '@')
                        item.Name = Uniconta.ClientTools.Localization.lookup(name.Substring(1)); ;
                }

                if (item is GridDashboardItem)
                {
                    var grid = dasdboard.Items[item.ComponentName] as GridDashboardItem;
                    if (grid != null)
                    {
                        var targetLst = grid.Columns.OfType<GridDimensionColumn>().ToList();
                        foreach (var col in targetLst)
                        {
                            var colName = ((GridDimensionColumn)col).Dimension.Name;
                            var savedName = col.CustomProperties.GetValue(col.Dimension.UniqueId) ?? string.Empty;

                            if ((colName != null || !string.IsNullOrEmpty(colName)) && (col.Dimension.Name.StartsWith("&") || col.Dimension.Name.StartsWith("@") || savedName.StartsWith("@") || savedName.StartsWith("&")))
                            {
                                if (col.CustomProperties.GetValue(col.Dimension.UniqueId) == null)
                                    col.CustomProperties.SetValue(col.Dimension.UniqueId, name);
                                else
                                    colName = savedName;
                                ((GridDimensionColumn)col).Dimension.Name = Uniconta.ClientTools.Localization.lookup(colName.Substring(1));
                            }
                        }
                    }
                }

                if (item is PivotDashboardItem)
                {

                    var pivot = dasdboard.Items[item.ComponentName] as PivotDashboardItem;
                    if (pivot != null)
                    {
                        var cols = pivot.Columns.OfType<DataItem>().ToList();
                        if (cols?.Count() > 0)
                            SetLabelValues(pivot, cols);
                        var rows = pivot.Rows.OfType<DataItem>().ToList();
                        if (rows?.Count() > 0)
                            SetLabelValues(pivot, rows);
                        var values = pivot.Values.OfType<DataItem>().ToList();
                        if (values?.Count() > 0)
                            SetLabelValues(pivot, values);
                    }
                }

                if (item is ChartDashboardItem)
                {
                    var chart = dasdboard.Items[item.ComponentName] as ChartDashboardItem;
                    if (chart != null)
                    {
                        var dimensions = chart.GetDimensions()?.OfType<DataItem>().ToList();
                        if (dimensions?.Count() > 0)
                            SetChartLabelValues(chart, dimensions);
                        var measures = chart.GetMeasures()?.OfType<DataItem>().ToList();
                        if (measures?.Count() > 0)
                            SetChartLabelValues(chart, measures);
                    }
                }

                if (item is PieDashboardItem)
                {
                    var pie = dasdboard.Items[item.ComponentName] as PieDashboardItem;
                    if (pie != null)
                    {
                        var dimensions = pie.GetDimensions()?.OfType<DataItem>().ToList();
                        if (dimensions?.Count() > 0)
                            SetPieLabelValues(pie, dimensions);
                        var measures = pie.GetMeasures()?.OfType<DataItem>().ToList();
                        if (measures?.Count() > 0)
                            SetPieLabelValues(pie, measures);
                    }
                }

                var sctChart = dasdboard.Items[item.ComponentName] as ScatterChartDashboardItem;
                if (sctChart != null)
                {
                    var measures = sctChart.GetMeasures()?.OfType<DataItem>().ToList();
                    if (measures?.Count() > 0)
                        SetScatterChartLabelValues(sctChart, measures);
                }
            }

            if (!dasdboard.Title.ShowMasterFilterState)
                rowFilter.Visibility = Visibility.Collapsed;

            XElement data = e.Dashboard.UserData;
            if (data != null)
            {
                try
                {
                    var state = data.Element("DashboardState");
                    if (state != null)
                        dState.LoadFromXml(XDocument.Parse(state.Value));
                }
                catch { }

                var fixedComps = data.Element("FixedCompanies");
                if (fixedComps != null)
                {
                    fixedCompanies = null;
                    var rootelement = XElement.Parse(fixedComps.ToString());
                    foreach (var el in rootelement.Elements())
                    {
                        var fxdComps = XDocument.Parse(el.ToString()).Elements("FixedCompany")
                             .Select(p => new FixedCompany
                             {
                                 CompanyId = int.Parse(p.Element("CompanyId").Value),
                                 DatasourceName = p.Element("DatasourceName").Value
                             }).ToList();
                        if (fixedCompanies != null)
                            fixedCompanies.AddRange(fxdComps);
                        else
                            fixedCompanies = fxdComps;
                    }

                    if (fixedCompanies != null)
                    {
                        for (int i = 0; i < fixedCompanies.Count; i++)
                        {
                            var comp = CWDefaultCompany.loadedCompanies.FirstOrDefault(x => x.CompanyId == fixedCompanies[i].CompanyId) as Company;
                            var openComp = OpenFixedCompany(comp).GetAwaiter().GetResult();
                            if (openComp != null)
                                LoadListOfTableTypes(openComp);
                            else
                                UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup("UserNoAccessToCompany"), Uniconta.ClientTools.Localization.lookup("Information"));
                        }
                    }
                }

                var ldOnOpn = data.Element("LoadOnOpen");
                if (ldOnOpn != null)
                    LoadOnOpen = bool.Parse(ldOnOpn.Value);
                var tlTxt = data.Element("TitleText");
                if (tlTxt != null)
                    titleText = tlTxt.Value;
                var userId = data.Element("LogedInUserIdFilter");
                if (userId != null)
                    UserIdPropName = userId.Value;

                #region Dashboard user fields

                var dshbdUserFls = data.Element("DashboardUserFields");
                if (dshbdUserFls != null)
                {
                    var nodes = dshbdUserFls.Nodes();
                    dashboardUserFields = new Dictionary<string, List<DashboardUserField>>();
                    foreach (var node in nodes)
                    {
                        if (node is XElement)
                        {
                            var name = ((XElement)node).Name;
                            var rt = XElement.Parse(node.ToString());
                            var userLst = new List<DashboardUserField>();
                            foreach (var el in rt.Elements())
                            {
                                var xml = el.ToString();
                                var fxdComps = XDocument.Parse(xml).Elements("DashboardUserField")
                                     .Select(p => new DashboardUserField
                                     {
                                         FieldName = p.Element("FieldName").Value,
                                         Value = p.Element("Value").Value,
                                         DataType = (byte)(p.Element("DataType") != null ? Convert.ToByte(p.Element("DataType").Value) : 0)
                                     }).ToList();
                                userLst.AddRange(fxdComps);
                            }
                            dashboardUserFields.Add(name.ToString(), userLst);
                        }
                    }
                }
                #endregion
            }
            else
                LoadOnOpen = true;  // for old saved dashboards

            if (LoadOnOpen)
                foreach (var ds in e.Dashboard.DataSources)
                    dataSourceLoadingParams.Add(ds.ComponentName);
        }

        private List<PropValuePair> GetPropValuePairForDataSource(Type TableType, List<FilterProperties> filterProps)
        {
            List<PropValuePair> propPairLst = new List<PropValuePair>();
            Filter[] filters = filterProps.Select(p => new Filter() { name = p.PropertyName, value = p.UserInput, parameterType = p.ParameterType }).ToArray();
            var filterSorthelper = new FilterSortHelper(TableType, filters, null);
            List<string> errors;
            propPairLst = filterSorthelper.GetPropValuePair(out errors);
            return propPairLst;
        }

        DashboardItem FocusedItem;
        private void DashboardLayoutItem_MouseUp(object sender, MouseButtonEventArgs e)
        {
            DevExpress.DashboardWpf.DashboardLayoutItem item = sender as DevExpress.DashboardWpf.DashboardLayoutItem;
            FocusedItem = dashboardViewerUniconta.Dashboard.Items[item.Name];
            if (FocusedItem != null && (((DataDashboardItem)FocusedItem).DataSource) != null && dataSourceAndTypeMap.ContainsKey((((DataDashboardItem)FocusedItem).DataSource).ComponentName))
            {
                tableType = dataSourceAndTypeMap[(((DataDashboardItem)FocusedItem).DataSource).ComponentName];
                selectedDataSourceName = (((DataDashboardItem)FocusedItem).DataSource).ComponentName;
                var dataSourceName = (((DataDashboardItem)FocusedItem).DataSource).Name;
                int startindex = dataSourceName.IndexOf('(');
                if (startindex >= 0)
                {
                    int Endindex = dataSourceName.IndexOf(')');
                    string companyName = dataSourceName.Substring(startindex + 1, Endindex - startindex - 1);
                    companyName = string.Compare(companyName, company.KeyName) == 0 ? companyName : company.KeyName;
                    if (string.IsNullOrEmpty(titleText))
                        dashboardViewerUniconta.TitleContent = string.Format("{0} {1} ( {2} )", Uniconta.ClientTools.Localization.lookup("Company"), companyName, tableType.Name);
                }
                else
                {
                    if (string.IsNullOrEmpty(titleText))
                        dashboardViewerUniconta.TitleContent = string.Format("{0} {1} ( {2} )", Uniconta.ClientTools.Localization.lookup("Company"), company.KeyName, tableType.Name);
                }
            }
            else if (((DataDashboardItem)FocusedItem).DataSource is DashboardFederationDataSource)
            {
                var dataSource = ((DataDashboardItem)FocusedItem).DataSource;
                var queryTable = ((DevExpress.DataAccess.DataFederation.FederationDataSourceBase)dataSource)?.Queries?.FirstOrDefault();
                var sources = ((DevExpress.DataAccess.DataFederation.FederationDataSourceBase)(dataSource)).Sources;
                var name = StringBuilderReuse.Create();
                name.Append(_selectedDashBoard._Name).Append(" (");
                foreach (var source in sources)
                {
                    if (source.DataSource is DashboardObjectDataSource)
                    {
                        var componentName = ((DashboardObjectDataSource)(source.DataSource)).ComponentName;
                        name.Append(source.Name).Append("  ");
                        if (!dataSourceLoadingParams.Contains(componentName))
                            dataSourceLoadingParams.Add(componentName);

                        if (string.Compare(queryTable?.ToString(), source.Name) == 0)
                        {
                            selectedDataSourceName = componentName;
                            tableType = dataSourceAndTypeMap[selectedDataSourceName];
                        }
                    }
                }
                name.Append(')');
                //if (string.IsNullOrEmpty(titleText))
                //    dashboardViewerUniconta.TitleContent = name.ToStringAndRelease();
                if (!LoadOnOpen)          // load on open is saved as true. and first time on load user click on federation related dashboardItem 
                    LoadOnOpen = true;
                dashboardViewerUniconta.ReloadData();
            }
            else
            {
                tableType = null;
                selectedDataSourceName = null;
                if (string.IsNullOrEmpty(titleText))
                    dashboardViewerUniconta.TitleContent = string.Empty;
            }
        }

        public static DateTime MakeDate(long ticks) { return new DateTime(ticks); }
        public static Type GeneraterUserType(Type BaseType, List<DashboardUserField> flds)
        {
            try
            {
                AssemblyName an = new AssemblyName("UserAssembly_" + BaseType.Name);
                AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
                ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

                TypeBuilder builder = moduleBuilder.DefineType(BaseType.Name + "Dashboard",
                                    TypeAttributes.Public |
                                    TypeAttributes.Class |
                                    TypeAttributes.AutoClass |
                                    TypeAttributes.AnsiClass |
                                    TypeAttributes.BeforeFieldInit |
                                    TypeAttributes.AutoLayout,
                                    BaseType);
                foreach (var fld in flds)
                {
                    Type t;
                    switch (fld.DataType)
                    {
                        case 0: // string
                            t = typeof(string);
                            break;
                        case 1: // int
                            t = typeof(int);
                            break;
                        case 2: // double
                            t = typeof(double);
                            break;
                        case 3: // date time
                            t = typeof(DateTime);
                            break;
                        case 4: // bool
                            t = typeof(bool);
                            break;
                        default: // long
                            t = typeof(long);
                            break;
                    }
                    var getMethodBuilder =
                        builder.DefineMethod("get_" + fld.FieldName,
                            MethodAttributes.Public |
                            MethodAttributes.SpecialName |
                            MethodAttributes.HideBySig,
                            t, Type.EmptyTypes);

                    var getIL = getMethodBuilder.GetILGenerator();

                    switch (fld.DataType)
                    {
                        case 0: // string
                            getIL.Emit(OpCodes.Ldstr, fld.Value);
                            break;
                        case 1: // int
                            getIL.Emit(OpCodes.Ldc_I4, (int)NumberConvert.ToInt(fld.Value));
                            break;
                        case 2: // double
                            getIL.Emit(OpCodes.Ldc_R8, NumberConvert.ToDoubleNoThousandSeperator(fld.Value));
                            break;
                        case 3: // date time
                            getIL.Emit(OpCodes.Ldc_I8, StringSplit.DateParse(fld.Value, 0).Ticks);
                            getIL.Emit(OpCodes.Call, typeof(DashBoardViewerPage).GetMethod("MakeDate"));
                            break;
                        case 4: // bool
                            getIL.Emit((fld.Value == "1" || string.Compare(fld.Value, "true", StringComparison.OrdinalIgnoreCase) == 0) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                            break;
                        default: // long
                            getIL.Emit(OpCodes.Ldc_I8, NumberConvert.ToInt(fld.Value));
                            break;
                    }

                    getIL.Emit(OpCodes.Ret);

                    var propertyBuilder = builder.DefineProperty(fld.FieldName, System.Reflection.PropertyAttributes.HasDefault, t, null);
                    propertyBuilder.SetGetMethod(getMethodBuilder);
                }
                return builder.CreateType();
            }
            catch
            {
                return null;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            dashboardViewerUniconta.ReloadData();
        }

        bool isDashBoardLaoded;
        private void DashboardViewerUniconta_Loaded(object sender, RoutedEventArgs e)
        {
            if (!isDashBoardLaoded)
            {
                isDashBoardLaoded = true;
                Initialise();
            }
            if (isDashBoardLaoded && timer != null)
                timer.Start();
        }

        private void DashboardViewerUniconta_Unloaded(object sender, RoutedEventArgs e)
        {
            if (isDashBoardLaoded && timer != null)
                timer.Stop();
        }

        private async Task<bool> Initialise()
        {
            busyIndicator.IsBusy = true;
            if (_selectedDashBoard == null)
            {
                int rowId;
                int.TryParse(dashboardName, out rowId);
                var dbrdLst = await api.Query<DashboardClient>() as IEnumerable<UserReportDevExpressClient>;
                if (rowId != 0)
                    _selectedDashBoard = dbrdLst?.FirstOrDefault(x => x.RowId == rowId) as DashboardClient;
                else
                    _selectedDashBoard = dbrdLst?.FirstOrDefault(x => string.Compare(x._Name, dashboardName, StringComparison.CurrentCultureIgnoreCase) == 0) as DashboardClient;
            }
            if (_selectedDashBoard != null)
            {
                var result = await api.Read(_selectedDashBoard);
                if (result == ErrorCodes.Succes)
                {
                    if (_selectedDashBoard.Layout != null)
                        ReadDataFromDB(_selectedDashBoard.Layout);
                    dashboardViewerUniconta.TitleContent = !string.IsNullOrEmpty(titleText) ? titleText : _selectedDashBoard.Name;
                }
                else
                    UnicontaMessageBox.Show(Uniconta.ClientTools.Localization.lookup(result.ToString()), Uniconta.ClientTools.Localization.lookup("Error"));
            }

            if (_selectedDashBoard == null)
                busyIndicator.IsBusy = false;
            return true;
        }

        private bool ReadDataFromDB(byte[] selectedDashBoardBinary)
        {
            busyIndicator.IsBusy = true;
            bool retVal = true;
            try
            {
                lstOfNewFilters.Clear();
                var customReader = StreamingManagerReuse.Create(selectedDashBoardBinary);
                var version = customReader.readByte();
                if (version < 1 || version > 3)
                    return false;

                var bufferedReport = StreamingManager.readMemory(customReader);
                var st = Compression.Uncompress(bufferedReport);

                if (customReader.readBoolean())
                {
                    int filterCount = (int)customReader.readNum();
                    if (version < 3)
                    {
                        for (int i = 0; i < filterCount; i++)
                        {
                            var key = customReader.readString();
                            List<FilterProperties> arrFilter;
                            SortingProperties[] sortProps;
                            FilterSortHelper.ConvertPropValuePair((PropValuePair[])customReader.ToArray(typeof(PropValuePair)), out arrFilter, out sortProps);
                            lstOfNewFilters.Add(key, arrFilter);
                        }
                    }
                    else if (version == 3)
                    {
                        for (int i = 0; i < filterCount; i++)
                        {
                            var key = customReader.readString();
                            var arrFilter = (FilterProperties[])customReader.ToArray(typeof(FilterProperties));
                            lstOfNewFilters.Add(key, arrFilter.ToList());
                        }
                    }
                }
                if (customReader.readBoolean())
                {
                    int sortCount = (int)customReader.readNum();
                    for (int i = 0; i < sortCount; i++)
                    {
                        var key = customReader.readString();
                        var arrSort = (SortingProperties[])customReader.ToArray(typeof(SortingProperties));
                        FilterSorter propSort = new FilterSorter(arrSort);
                        if (lstOfSorters != null && !lstOfSorters.ContainsKey(key))
                            lstOfSorters.Add(key, propSort);
                    }
                }
                customReader.Release();

                XDocument xdoc = XDocument.Load(st);
                var element = xdoc.Root.Attributes().Where(x => x.Name == "RefreshTimer").FirstOrDefault();
                if (element != null)
                {
                    int refreshTimer = (int)NumberConvert.ToInt(element.Value);
                    if (refreshTimer > 0)
                    {
                        if (timer == null)
                            timer = new System.Windows.Forms.Timer();
                        timer.Interval = refreshTimer < 300 ? 300 * 1000 : refreshTimer * 1000;
                        timer.Tick += Timer_Tick;
                        timer.Start();
                    }
                }
                ShowBusyIndicator();
                st.Seek(0, System.IO.SeekOrigin.Begin);
                dashboardViewerUniconta.LoadDashboard(st);
                st.Release();
                return retVal;
            }
            catch (Exception ex)
            {
                ClearBusy();
                UnicontaMessageBox.Show(ex);
                CloseDockItem();
                return false;
            }
        }

        void ShowBusyIndicator()
        {
            DispatcherTimer timer = new DispatcherTimer();
            EventHandler tick = null;
            tick = (o, eventArgs) =>
            {
                if (dashboardViewerUniconta.DashboardViewModel != null && !dashboardViewerUniconta.DashboardViewModel.IsLoaded)
                    return;
                ClearBusy();
                timer.Tick -= tick;
                timer.Stop();
            };
            timer.Tick += tick;
            timer.Start();
        }

        public Task<UnicontaBaseEntity[]> Query(Type type, CrudAPI crudApi, IEnumerable<UnicontaBaseEntity> masters, IEnumerable<PropValuePair> filterValue)
        {
            return Task.Run(async () =>
            {
                return await crudApi.Query(Activator.CreateInstance(type) as UnicontaBaseEntity, masters, filterValue);
            });
        }

        Dashboard _dashboard;
        private void DashboardViewerUniconta_DashboardChanged(object sender, EventArgs e)
        {
            _dashboard = dashboardViewerUniconta.Dashboard;
        }

        List<string> dataSourceLoadingParams = new List<string>();
        private void dashboardViewerUniconta_AsyncDataLoading(object sender, DataLoadingEventArgs e)
        {
            try
            {
                var dashboard = _dashboard?.DataSources?.FirstOrDefault(x => x.ComponentName == e.DataSourceComponentName);
                var fixedComp = fixedCompanies?.FirstOrDefault(x => x.DatasourceName == e.DataSourceComponentName);
                var compId = fixedComp != null ? fixedComp.CompanyId : this.company.CompanyId;
                var typeofTable = GetTypeAssociatedWithDashBoardDataSource(e.DataSourceName, e.DataSourceComponentName, compId);
                if (typeofTable == null && dataSourceAndTypeMap.ContainsKey(e.DataSourceComponentName))
                    typeofTable = dataSourceAndTypeMap[e.DataSourceComponentName];
                if (typeofTable != null)
                {
                    if (dashboard != null)
                    {
                        bool IsDashBoardTableTYpe = false;
                        Type dashbaordTableType = null;
                        if (lstOfNewFilters.ContainsKey(e.DataSourceComponentName))
                            filterValues = UtilDisplay.GetPropValuePair(lstOfNewFilters[e.DataSourceComponentName]);
                        else
                            filterValues = null;
                        if (!LoadOnOpen)
                            e.Data = Activator.CreateInstance(typeofTable);
                        else
                        {
                            if (dataSourceLoadingParams.Contains(e.DataSourceComponentName) && LoadOnOpen)
                            {
                                UnicontaBaseEntity[] data;
                                var dbUserflds = dashboardUserFields?.FirstOrDefault(x => x.Key == e.DataSourceComponentName).Value;  // Dashbaord user field list for particular datasource
                                if (dbUserflds != null)
                                {
                                    var t = GeneraterUserType(typeofTable, dbUserflds);
                                    if (t != null)
                                    {
                                        dashbaordTableType = t;
                                        IsDashBoardTableTYpe = true;
                                    }
                                }
                                var masterRecords = GetMasterRecords(typeofTable);
                                CrudAPI compApi = null;
                                if (fixedComp == null || this.company.CompanyId == fixedComp.CompanyId)
                                    data = Query(!IsDashBoardTableTYpe ? typeofTable : dashbaordTableType, api, masterRecords, filterValues).GetAwaiter().GetResult();
                                else
                                {
                                    var comp = CWDefaultCompany.loadedCompanies.FirstOrDefault(x => x.CompanyId == fixedComp.CompanyId);
                                    compApi = new CrudAPI(BasePage.session, comp);
                                    data = Query(!IsDashBoardTableTYpe ? typeofTable : dashbaordTableType, compApi, masterRecords, filterValues).GetAwaiter().GetResult();
                                }

                                if (lstOfSorters != null && lstOfSorters.ContainsKey(e.DataSourceComponentName))
                                    Array.Sort(data.ToArray(), lstOfSorters[e.DataSourceComponentName]);

                                if (typeofTable.Equals(typeof(CompanyDocumentClient)))
                                    ReadData(data, compApi ?? api).GetAwaiter().GetResult(); // if there is image in property it will load again;

                                if (data != null)
                                    e.Data = BuildDataTable(data, !IsDashBoardTableTYpe ? typeofTable : dashbaordTableType, compApi ?? api);
                                else
                                    e.Data = data;
                            }
                            else
                                e.Data = Activator.CreateInstance(typeofTable);
                        }
                    }
                    if (TableContainLoginIdProp(typeofTable))
                    {
                        var filter = "[" + UserIdPropName + "] =" + "'" + BasePage.session.LoginId + "'";
                        dashboard.Filter = filter;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Uniconta.ClientTools.Localization.lookup("Exception"));
            }
        }

        public static DataTable BuildDataTable(IList<UnicontaBaseEntity> lst, Type entType, CrudAPI compApi, bool removeRefProps = false, bool showActualProps = false)
        {
            DataTable dataTable = CreateTable(entType, compApi, removeRefProps, showActualProps);
            PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(entType);
            foreach (UnicontaBaseEntity current in lst)
            {
                DataRow dataRow = dataTable.NewRow();

                foreach (PropertyDescriptor propertyDescriptor in properties)
                {
                    if (dataTable.Columns.Contains(propertyDescriptor.Name))
                        dataRow[propertyDescriptor.Name] = propertyDescriptor.GetValue(current) ?? DBNull.Value;
                }
                dataTable.Rows.Add(dataRow);
            }
            return dataTable;
        }

        private static DataTable CreateTable(Type entType, CrudAPI compApi, bool removeRefProps = false, bool showActualProps = false)
        {
            DataTable dataTable = new DataTable(entType.Name);
            var propInfos = entType.GetProperties();
            foreach (var prop in propInfos)
            {
                var col = CreateColumn(prop, showActualProps, removeRefProps, compApi);
                if (col != null)
                    dataTable.Columns.Add(col);
            }
            return dataTable;
        }

        static DataColumn CreateColumn(PropertyInfo prop, bool showActualProps, bool removeRefProps, CrudAPI compApi)
        {
            var column = new DataColumn();
            column.ColumnName = prop.Name;
            if (prop.GetCustomAttributes(typeof(ReportingAttribute), true).Length != 0 && !removeRefProps)
            {
                var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var userType = compApi.CompanyEntity?.GetUserType(propType);
                column.DataType = userType ?? propType;
            }
            else
                column.DataType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            if (!showActualProps)
            {
                var attribut = prop.GetCustomAttributes(typeof(DisplayAttribute), false);
                if (attribut != null && attribut.Count() > 0)
                {
                    var name = attribut?.Cast<DisplayAttribute>()?.Single()?.GetName();
                    column.Caption = name;
                }
            }
            return column;
        }
        public static Task<bool> ReadData(UnicontaBaseEntity[] data, CrudAPI api)
        {
            return Task.Run(async () =>
            {
                foreach (UnicontaBaseEntity row in data)
                {
                    foreach (PropertyInfo propertyInfo in row.GetType().GetProperties())
                    {
                        if (propertyInfo.PropertyType == typeof(byte[]))
                        {
                            await api.Read(row);
                        }
                    }
                }
                return true;
            });
        }

        List<UnicontaBaseEntity> GetMasterRecords(Type TableTYpe)
        {
            if (master == null || masterField == null)
                return null;
            List<UnicontaBaseEntity> masters = null;
            var instance = Activator.CreateInstance(TableTYpe) as UnicontaBaseEntity;
            PropertyInfo p = TableTYpe.GetProperty(masterField);
            if (p != null)
                masters = new List<UnicontaBaseEntity>() { master };
            return masters;
        }

        bool TableContainLoginIdProp(Type retType)
        {
            bool exist = false;
            if (!string.IsNullOrEmpty(UserIdPropName))
            {
                var prop = retType.GetProperty(UserIdPropName);
                if (prop != null)
                    exist = true;
            }
            return exist;
        }

        private Type GetTypeAssociatedWithDashBoardDataSource(string dataSource, string componentName, int companyId)
        {
            Type retType = null;
            var ds = _dashboard?.DataSources?.Where(d => d.ComponentName == componentName).FirstOrDefault();
            if (ds != null)
            {
                var data = ds.Data;
                string tblType = null;
                PropertyInfo propInfo = null;
                string fullname = null;
                if (data != null)
                {
                    propInfo = ds.Data.GetType().GetProperty("FullName");
                    if (propInfo != null)
                        fullname = propInfo.GetValue(ds.Data, null) as string;
                    if (fullname == null)
                    {
                        var li = ds.Name.IndexOf('(');
                        tblType = li >= 0 ? ds.Name.Substring(0, li) : ds.Name;
                        fullname = string.Concat("Uniconta.ClientTools.DataModel.", tblType);
                    }
                }
                else
                {
                    var li = ds.Name.IndexOf('(');
                    tblType = li >= 0 ? ds.Name.Substring(0, li) : ds.Name;
                    fullname = string.Concat("Uniconta.ClientTools.DataModel.", tblType);
                }

                if (fullname != null)
                {
                    fullname = fullname.Replace("[]", "");
                    var tables = ListOfReportTableTypes.FirstOrDefault(x => x.Key == companyId).Value;
                    retType = tables?.Where(p => p.FullName == fullname).FirstOrDefault();
                    if (retType == null)
                        retType = tables?.Where(p => p.FullName == dataSource).FirstOrDefault();
                    if (retType == null)
                        retType = tables?.Where(p => p.FullName == tblType).FirstOrDefault();
                    if (retType != null)
                    {
                        if (!dataSourceAndTypeMap.ContainsKey(componentName))
                        {
                            dataSourceAndTypeMap.Add(componentName, retType);
                            tableType = retType;
                        }
                    }
                }
            }
            return retType;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFilterDialog();
        }

        public void OpenFilterDialog()
        {
            try
            {
                if (!string.IsNullOrEmpty(selectedDataSourceName) && tableType != null)
                {
                    List<FilterProperties> filterProps;
                    if (lstOfNewFilters.ContainsKey(selectedDataSourceName))
                        filterProps = lstOfNewFilters[selectedDataSourceName];
                    else
                        filterProps = null;

                    if (lstOfSorters.ContainsKey(selectedDataSourceName))
                        PropSort = lstOfSorters[selectedDataSourceName];
                    else
                        PropSort = null;
                    Filter[] filters = null;
                    SortingProperties[] sorters = null;
                    //if (filterValues != null)
                    //    filters = Utility.CreateDefaultFilter(filterValues, tableType);
                    if (filterValues != null)
                        filters = filterProps.Select(p => new Filter() { name = p.PropertyName, value = p.UserInput, parameterType = p.ParameterType }).ToArray();
                    if (PropSort != null)
                        sorters = Utility.CreateDefaultSort(PropSort);
                    var fixedComp = fixedCompanies?.FirstOrDefault(x => x.DatasourceName == selectedDataSourceName);
                    if (fixedComp == null || this.company.CompanyId == fixedComp?.CompanyId)
                        filterDialog = new CWServerFilter(api, tableType, filters, sorters, null);
                    else
                    {
                        var comp = CWDefaultCompany.loadedCompanies.FirstOrDefault(x => x.CompanyId == fixedComp.CompanyId) as Company;
                        var compApi = new CrudAPI(api.session, comp);
                        filterDialog = new CWServerFilter(compApi, tableType, filters, sorters, null);
                    }
                    filterDialog.Closing += FilterDialog_Closing;
                    filterDialog.Show();
                }
            }
            catch (Exception ex)
            {
                UnicontaMessageBox.Show(ex);
            }
        }

        private void FilterDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (filterDialog.DialogResult == true)
            {
                var filtersProps = filterDialog.Filters.Select(p => new FilterProperties() { PropertyName = p.name, UserInput = p.value, ParameterType = p.parameterType });
                filterValues = filterDialog.PropValuePair;
                PropSort = filterDialog.PropSort;

                if (lstOfNewFilters.ContainsKey(selectedDataSourceName))
                {
                    if (filtersProps == null || filtersProps.Count() == 0)
                        lstOfNewFilters.Remove(selectedDataSourceName);
                    else
                        lstOfNewFilters[selectedDataSourceName] = filtersProps?.ToList();
                }
                else
                {
                    if (filtersProps != null && filtersProps.Count() > 0)
                        lstOfNewFilters.Add(selectedDataSourceName, filtersProps?.ToList());
                }

                if (lstOfSorters.ContainsKey(selectedDataSourceName))
                {
                    if (PropSort == null || PropSort.sortingProperties.Count() == 0)
                        lstOfSorters.Remove(selectedDataSourceName);
                    else
                        lstOfSorters[selectedDataSourceName] = PropSort;
                }
                else
                {
                    if (PropSort != null && PropSort.sortingProperties.Count() > 0)
                        lstOfSorters.Add(selectedDataSourceName, PropSort);
                }
                LoadOnOpen = true;
                DataDashboardItem dataItem = FocusedItem as DataDashboardItem;
                if (dataItem == null) return;
                string dsComponentName = dataItem.DataSource.ComponentName;
                if (!dataSourceLoadingParams.Contains(dsComponentName))
                    dataSourceLoadingParams.Add(dsComponentName);
                dashboardViewerUniconta.ReloadData();
            }
            e.Cancel = true;
            filterDialog.Hide();
        }

        private void btnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(selectedDataSourceName))
            {
                if (lstOfSorters.ContainsKey(selectedDataSourceName))
                    lstOfSorters.Remove(selectedDataSourceName);
                if (lstOfNewFilters.ContainsKey(selectedDataSourceName))
                    lstOfNewFilters.Remove(selectedDataSourceName);
                dashboardViewerUniconta.ReloadData();
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            dashboardViewerUniconta.ReloadData();
        }

        void LoadListOfTableTypes(Company company)
        {
            if (company != null)
            {
                var list = Global.GetTables(company);
                var userTables = Global.GetUserTables(company);
                if (userTables != null)
                    list.AddRange(userTables);
                if (!ListOfReportTableTypes.ContainsKey(company.CompanyId))
                    ListOfReportTableTypes.Add(company.CompanyId, list.ToArray());
            }
        }

        Task<Company> OpenFixedCompany(Company comp)
        {
            return Task.Run(async () =>
            {
                return BasePage.session.GetOpenCompany(comp.CompanyId) ?? await BasePage.session.OpenCompany(comp.CompanyId, false, comp);
            });
        }

        private void btnWinSettg_Click(object sender, RoutedEventArgs e)
        {
            string name = this.ParentControl != null ? Convert.ToString(this.ParentControl.Tag) : NameOfControl;
            var dockSettingDialog = new CWControlDockSetting(name);
            dockSettingDialog.Show();
        }
    }

    public class FixedCompany
    {
        public int CompanyId { get; set; }
        public string DatasourceName { get; set; }
    }

    public class DashboardUserField
    {
        public string FieldName { get; set; }
        public string Value { get; set; }

        public byte DataType { get; set; }
    }

    public class PivotBehavior : Behavior<PivotGridControl>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            ((PivotGridControl)AssociatedObject).DataSourceChanged += PivotBehavior_DataSourceChanged;
        }
        protected override void OnDetaching()
        {
            ((PivotGridControl)AssociatedObject).DataSourceChanged -= PivotBehavior_DataSourceChanged;
            base.OnDetaching();
        }
        private void PivotBehavior_DataSourceChanged(object sender, RoutedEventArgs e)
        {
            AssociatedObject.BestFitMaxRowCount = 100;
            ((PivotGridControl)AssociatedObject).BestFit();
        }
    }
}
