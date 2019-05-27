using System;
using System.Collections.Generic;
using System.Linq;
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
using Uniconta.API.System;
using Uniconta.ClientTools;
using Uniconta.ClientTools.DataModel;
using Uniconta.DataModel;

using UnicontaClient.Pages;
namespace UnicontaClient.Pages.CustomPage
{
    public partial class CwSelectStandardTable : ChildWindow
    {
        CrudAPI api;
        public Type table;
        public CwSelectStandardTable(CrudAPI api) : this(api, false)
        {
        }
        public CwSelectStandardTable(CrudAPI api, bool onlyIdKeyTables)
        {
            this.DataContext = this;
            InitializeComponent();
            this.Title = Uniconta.ClientTools.Localization.lookup("Table");
            this.api = api;
            if (onlyIdKeyTables)
                bindRefTable();
            else
                bindTablelist();
        }
        private void bindTablelist()
        {
            var xlist = new List<TableList>();
            List<Type> tablestype = Global.GetTables(api.CompanyEntity);
            foreach (var type in tablestype)
            {
                var clientTableAttr = type.GetCustomAttributes(typeof(ClientTableAttribute), true);
                if (clientTableAttr != null && clientTableAttr.Any())
                {
                    var attr = (ClientTableAttribute)clientTableAttr[0];
                    if (attr.CanUpdate)
                        xlist.Add(new TableList() { Name = string.Format("{0} ({1})", type.Name, Uniconta.ClientTools.Localization.lookup(attr.LabelKey)), Type = type });
                }
                else
                    xlist.Add(new TableList() { Name = type.Name, Type = type });
            }
            cmbStdTables.ItemsSource = xlist.OrderBy(x => x.Name).ToList();
            cmbStdTables.SelectedIndex = 0;
        }
        private void bindRefTable()
        {
            var xlist = new List<TableList>();
            foreach (var type in Global.GetStandardUserRefTables())
            {
                var clientTableAttr = type.GetCustomAttributes(typeof(ClientTableAttribute), true);
                if (clientTableAttr != null && clientTableAttr.Any())
                {
                    if (type == typeof(DebtorOrderClient) || type == typeof(DebtorOfferClient) || type == typeof(CreditorOrderClient) || type == typeof(ProductionOrderClient))
                        continue;
                    var attr = (ClientTableAttribute)clientTableAttr[0];
                    if (attr.CanUpdate)
                        xlist.Add(new TableList() { Name = string.Format("{0} ({1})", type.Name, Uniconta.ClientTools.Localization.lookup(attr.LabelKey)), Type = type });
                }
                else
                    xlist.Add(new TableList() { Name = type.Name, Type = type });
            }
            cmbStdTables.ItemsSource = xlist.OrderBy(x => x.Name).ToList();
            cmbStdTables.SelectedIndex = 0;
        }
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTable = cmbStdTables.SelectedItem as TableList;
            table = selectedTable?.Type;
            if (selectedTable != null)
                this.DialogResult = true;
            else
                this.DialogResult = false;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }

    public class TableList
    {
        public Type Type { get; set; }
        public string Name { get; set; }
    }
}