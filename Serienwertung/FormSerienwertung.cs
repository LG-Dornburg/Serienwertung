using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using System.Reflection;
using ClosedXML.Excel;

namespace Serienwertung
{

    public partial class FormSerienwertung : Form
    {
        private DataTable[] dTraw;
        private DataTable _dtResults = null;
        private string fieldname = "Wettbew-Name";
        private int _selected_Column;

        #region Member Variables for printing

        StringFormat strFormat; //Used to format the grid rows.
        ArrayList arrColumnLefts = new ArrayList();//Used to save left coordinates of columns
        ArrayList arrColumnWidths = new ArrayList();//Used to save column widths
        int iCellHeight = 0; //Used to get/set the datagridview cell height
        int iTotalWidth = 0; //
        int iRow = 0;//Used as counter
        bool bFirstPage = false; //Used to check whether we are printing first page
        bool bNewPage = false;// Used to check whether we are printing a new page
        int iHeaderHeight = 0; //Used for the header height
        DataGridView dgvPrint;

        #endregion


        public FormSerienwertung()
        {
            InitializeComponent();
            // add version to frame window
            this.Text += " - ver. " + Application.ProductVersion;
            // show main window           
            this.Show();

            // set alternating row colors
            this.dgv.RowsDefaultCellStyle.BackColor = Color.White;
            this.dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.LightGray;

            // recolor row if unkown time format
            this.dgv.RowPrePaint += new System.Windows.Forms.DataGridViewRowPrePaintEventHandler(this.dgv_RowPrePaint);

            // start import of csv
            importToolStripMenuItem.PerformClick();   
        }


        // import data from CSV files
        private void importCSV(object sender, EventArgs e)
        {
            // file dialog
            this.openFileDialog.Filter = "*.csv|*.csv";
            this.openFileDialog.FilterIndex = 1;
            this.openFileDialog.RestoreDirectory = true;
            this.openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

            // Allow the user to select multiple files.
            this.openFileDialog.Multiselect = true;
            this.openFileDialog.Title = "Select CSV files";
            this.openFileDialog.FileName = "";
            this.openFileDialog.ShowDialog();
        }

        private void openFileDialog_FileOk(object sender, CancelEventArgs e)
        {
            if (openFileDialog.FileNames.Length < 2)
            {
                MessageBox.Show("Es müssen mindestens 2 Dateien ausgewählt werden",
                "Warnung",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning // for Warning  
                                       //MessageBoxIcon.Error // for Error 
                                       //MessageBoxIcon.Information  // for Information
                                       //MessageBoxIcon.Question // for Question
                );
                return;
            }

            // reset data sources
            dgv.DataSource = null;


            // define Tables
            dTraw = new DataTable[openFileDialog.FileNames.Length];

            toolStripComboBoxStartNo.Items.Clear();
            // Read the files
            int i;
            for (i = 0; i < openFileDialog.FileNames.Length; i++)
            {
                toolStripComboBoxStartNo.Items.Add(Path.GetFileNameWithoutExtension(openFileDialog.FileNames[i]));
                // save datatable
                dTraw[i] = GetTableFromCSV(openFileDialog.FileNames[i]);
            }

            toolStripComboBoxStartNo.SelectedIndex = i-1;
           //_dtResults = getSeriesRunnerResults();



            // show wettkämpfe in combobox
            //var contests = (from DataRow dRow in _dtResults.Rows orderby dRow[fieldname] select dRow[fieldname]).Distinct();
            var contests = (from DataRow dRow in dTraw[0].Rows select dRow[fieldname]).Distinct();


            toolStripComboBoxWettbewerb.Items.Clear();
            foreach (string element in contests)
            {
                toolStripComboBoxWettbewerb.Items.Add(element);
            }


            if (_dtResults != null)
            {
                if (toolStripComboBoxWettbewerb.Items.Count > 0)
                {
                    //select last item
                    toolStripComboBoxWettbewerb.SelectedIndex = toolStripComboBoxWettbewerb.Items.Count - 1;
                }
            }

        }


        private DataTable GetTableFromCSV(string fName)
        {
            DataTable dt = new DataTable();

            // open csv in read-only 
            // files doesn't get locked and can be opend in other application e.g. excel
            FileStream fs = new FileStream(fName,
                                   FileMode.Open,
                                   FileAccess.Read,
                                   FileShare.ReadWrite);

            StreamReader csv_file = new StreamReader(fs, Encoding.Default, true);

            // add cloumns to table
            string line = csv_file.ReadLine();
            string[] coloumns = line.Split(';');

            foreach (string element in coloumns)
            {
                dt.Columns.Add(element, System.Type.GetType("System.String"));
            }

            // fill datatable
            while (csv_file.Peek() >= 0)
            {
                // read and add a line
                line = csv_file.ReadLine();
                string[] vals = line.Split(';');

                DataRow dr = dt.NewRow();

                for (int i = 0; i < vals.Length; i++)
                {
                    dr[coloumns[i]] = vals[i];
                }

                // add the line
                dt.Rows.Add(dr);
            }
            csv_file.Close();

            return dt;
        }

        private void toolStripComboBoxWettbewerb_SelectedIndexChanged(object sender, EventArgs e)
        {
            viewResultsDGV();
        }

        private void viewResultsDGV()
        {
            if (toolStripComboBoxWettbewerb.SelectedItem != null)
            {
                //filter results by "Wettbewerb" and sort ascending
                DataView dv = new DataView(_dtResults);
                dv.RowFilter = "([" + fieldname + "]  = '" + toolStripComboBoxWettbewerb.SelectedItem.ToString() + "')";

                // sort data by lastname and surename
                dv.Sort = "Name ASC, Vorname ASC";

                // display data in datagridview
                dgv.DataSource = dv.ToTable();

                showDefaultToolStripMenuItem.PerformClick();
                dgv.Refresh();
            }
        }


        private DataTable getSeriesRunnerResults()
        {
            string[] timeStr;
            // clone table for runners data
            DataTable dt = dTraw[toolStripComboBoxStartNo.SelectedIndex].Clone();
                   
            // add columns for results
            for (int i = 0; i < openFileDialog.FileNames.Length; i++)
            { 
                dt.Columns.Add(Path.GetFileNameWithoutExtension(openFileDialog.FileNames[i]));
            }
            // add cloumn for series result
            dt.Columns.Add("Serie");

            foreach (DataRow row in dTraw[toolStripComboBoxStartNo.SelectedIndex].AsEnumerable())
            {
                timeStr = new string[openFileDialog.FileNames.Length];
                //timeStr[0] = row["Zeit"].ToString().Trim(new Char[] { '\"' });

                for (int i = 0; i < openFileDialog.FileNames.Length; i++)
                {
                    
                    DataRow[] result = dTraw[i].Select("Vorname = '" + row["Vorname"].ToString() +
                                                    "' AND Name = '" + row["Name"].ToString() +
                                                "' AND Jahrgang = '" + row["Jahrgang"].ToString() +
                                              "' AND Wettbewerb = '" + row["Wettbewerb"].ToString() + "'");

                    if (result.Any())
                    {
                        // get times and cut of quotation marks
                        timeStr[i] = result[0]["Zeit"].ToString().Trim(new Char[] { '\"' });
                    }
                    else {break;}


                    //// break loop if no entry found
                    //if (string.IsNullOrEmpty(timeStr[i]))
                    //{
                       
                    //}

                }

                // if seriesrunner add times to table and calculate series time
                if (!string.IsNullOrEmpty(timeStr.Last()))
                {
                    TimeSpan SeriesTime = TimeSpan.Zero;
                    DataRow dr = dt.NewRow();
                    object[] rowArray = new object[dt.Columns.Count];
                    // copy row from table 
                    row.ItemArray.CopyTo(rowArray, 0);

                    TimeSpan time = new TimeSpan();
                    bool wrongFormat = false;

                    // add other run times
                    for (int i = 0; i < openFileDialog.FileNames.Length; i++)
                    {
                        time = TimesMethod(timeStr[i]);

                        if (time != TimeSpan.Zero)
                        {
                            rowArray[row.ItemArray.Count() + i] = time;
                            SeriesTime += time;
                        }
                        else
                        {
                            rowArray[row.ItemArray.Count() + i] = timeStr[i];
                            wrongFormat = true;
                        }
                    }

                    // set series time to zero if unkown time format
                    if (wrongFormat) { SeriesTime = TimeSpan.Zero; }


                    rowArray[dt.Columns.Count - 1] = SeriesTime;
                    // add the line
                    dr.ItemArray = rowArray;
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }




        private static TimeSpan TimesMethod(string timeStr)
        {
            // get last four letter of string to determine time format ("Min." or "Std.")
            string timeFormat = timeStr.Substring(timeStr.Length - 4, 4);
            timeStr = timeStr.Substring(0, timeStr.Length - 5);
            TimeSpan time = new TimeSpan();

            // parse string according time format
            if (timeFormat == "Min.")
            {
                time = TimeSpan.ParseExact(timeStr, @"m\:ss", null);
            }
            else if (timeFormat == "Std.")
            {
                time = TimeSpan.ParseExact(timeStr, @"h\:mm\:ss", null);
            }
            else
            {
                // set time to zero if time fomrat unkown
                time = TimeSpan.Zero;
            }
            return time;
        }

       
        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.saveFileDialog.Filter = "*.dat|*.dat|All files (*.*)|*.*";
            this.saveFileDialog.FilterIndex = 1;
            this.saveFileDialog.RestoreDirectory = true;
            this.saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
            this.saveFileDialog.Title = "Export to *.dat";
            this.saveFileDialog.ShowDialog();

            // skip save to file if dialog get chancled
            if (String.IsNullOrEmpty(this.saveFileDialog.FileName))
            {
                return;
            }

            // write dataSet to file
            using (StreamWriter sw = new StreamWriter(this.saveFileDialog.FileName))
            {
               
                DataTable dt = (DataTable)(dgv.DataSource);
                foreach (DataRow row in dt.Rows)           
                {
                    sw.Write(row["Start-Nr."].ToString() + ";"); 
                    sw.Write(row["Serie"].ToString()); 
                    sw.WriteLine(); // new line
                }
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // The user wants to exit the application. Close everything down.
            Application.Exit();
        }


        #region functions for printing 

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // copy dgv to print -> remove invisible columns
            dgvPrint = new DataGridView();
            dgvPrint.Hide();
            this.Controls.Add(dgvPrint);

            DataTable dt = DataGridView2DataTable(dgv);
            foreach (DataGridViewColumn col in dgv.Columns)
            {
                if (!col.Visible)
                {
                    dt.Columns.Remove(col.Name);
                }
            }

            dgvPrint.DataSource = dt;
           
            //Open the print dialog
            PrintDialog printDialog = new PrintDialog();            
            printDialog.Document = printDocument;
            printDialog.UseEXDialog = true;
            printDialog.Document.DefaultPageSettings.Landscape = true;
            
            //Get the document
            if (DialogResult.OK == printDialog.ShowDialog())
            {
                printDocument.DocumentName = "Serienwertung";             
                printDocument.Print();
            }
        }


        #region Begin Print Event Handler
        private void printDocument_BeginPrint(object sender, System.Drawing.Printing.PrintEventArgs e)
        {
            try
            {
                strFormat = new StringFormat();
                strFormat.Alignment = StringAlignment.Near;
                strFormat.LineAlignment = StringAlignment.Center;
                strFormat.Trimming = StringTrimming.EllipsisCharacter;

                arrColumnLefts.Clear();
                arrColumnWidths.Clear();
                iCellHeight = 0;
                iRow = 0;
                bFirstPage = true;
                bNewPage = true;

                // Calculating Total Widths
                iTotalWidth = 0;
                foreach (DataGridViewColumn dgvGridCol in dgvPrint.Columns)
                {
                    iTotalWidth += dgvGridCol.Width;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region Print Page Event
        private void printDocument_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
        {
            try
            {
                //Set the left margin
                int iLeftMargin = e.MarginBounds.Left;
                //Set the top margin
                int iTopMargin = e.MarginBounds.Top;
                //Whether more pages have to print or not
                bool bMorePagesToPrint = false;
                int iTmpWidth = 0;

                //For the first page to print set the cell width and header height
                if (bFirstPage)
                {
                    foreach (DataGridViewColumn GridCol in dgvPrint.Columns)
                    {
                        iTmpWidth = (int)(Math.Floor((double)((double)GridCol.Width /
                                       (double)iTotalWidth * (double)iTotalWidth *
                                       ((double)e.MarginBounds.Width / (double)iTotalWidth))));

                        iHeaderHeight = (int)(e.Graphics.MeasureString(GridCol.HeaderText,
                                    GridCol.InheritedStyle.Font, iTmpWidth).Height) + 11;

                        // Save width and height of headres
                        arrColumnLefts.Add(iLeftMargin);
                        arrColumnWidths.Add(iTmpWidth);
                        iLeftMargin += iTmpWidth;
                    }
                }
                //Loop till all the grid rows not get printed
                while (iRow <= dgvPrint.Rows.Count - 1)
                {
                    DataGridViewRow GridRow = dgvPrint.Rows[iRow];
                    //Set the cell height
                    iCellHeight = GridRow.Height + 5;
                    int iCount = 0;
                    //Check whether the current page settings allo more rows to print
                    if (iTopMargin + iCellHeight >= e.MarginBounds.Height + e.MarginBounds.Top)
                    {
                        bNewPage = true;
                        bFirstPage = false;
                        bMorePagesToPrint = true;
                        break;
                    }
                    else
                    {
                        if (bNewPage)
                        {   
                            //Draw Header
                            e.Graphics.DrawString(toolStripComboBoxWettbewerb.Text, new Font(dgvPrint.Font, FontStyle.Bold),
                                    Brushes.Black, e.MarginBounds.Left, e.MarginBounds.Top -
                                    e.Graphics.MeasureString(toolStripComboBoxWettbewerb.Text, new Font(dgvPrint.Font,
                                    FontStyle.Bold), e.MarginBounds.Width).Height - 13);

                            String strDate = DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToShortTimeString();
                            //Draw Date
                            e.Graphics.DrawString(strDate, new Font(dgvPrint.Font, FontStyle.Bold),
                                    Brushes.Black, e.MarginBounds.Left + (e.MarginBounds.Width -
                                    e.Graphics.MeasureString(strDate, new Font(dgvPrint.Font,
                                    FontStyle.Bold), e.MarginBounds.Width).Width), e.MarginBounds.Top -
                                    e.Graphics.MeasureString(toolStripComboBoxWettbewerb.Text, new Font(new Font(dgvPrint.Font,
                                    FontStyle.Bold), FontStyle.Bold), e.MarginBounds.Width).Height - 13);

                            //Draw Columns                 
                            iTopMargin = e.MarginBounds.Top;
                            foreach (DataGridViewColumn GridCol in dgvPrint.Columns)
                            {
                                e.Graphics.FillRectangle(new SolidBrush(Color.LightGray),
                                    new Rectangle((int)arrColumnLefts[iCount], iTopMargin,
                                    (int)arrColumnWidths[iCount], iHeaderHeight));

                                e.Graphics.DrawRectangle(Pens.Black,
                                    new Rectangle((int)arrColumnLefts[iCount], iTopMargin,
                                    (int)arrColumnWidths[iCount], iHeaderHeight));

                                e.Graphics.DrawString(GridCol.HeaderText, GridCol.InheritedStyle.Font,
                                    new SolidBrush(GridCol.InheritedStyle.ForeColor),
                                    new RectangleF((int)arrColumnLefts[iCount], iTopMargin,
                                    (int)arrColumnWidths[iCount], iHeaderHeight), strFormat);
                                iCount++;
                            }
                            bNewPage = false;
                            iTopMargin += iHeaderHeight;
                        }
                        iCount = 0;
                        //Draw Columns Contents                
                        foreach (DataGridViewCell Cel in GridRow.Cells)
                        {
                            if (Cel.Value != null)
                            {
                                e.Graphics.DrawString(Cel.Value.ToString(), Cel.InheritedStyle.Font,
                                            new SolidBrush(Cel.InheritedStyle.ForeColor),
                                            new RectangleF((int)arrColumnLefts[iCount], (float)iTopMargin,
                                            (int)arrColumnWidths[iCount], (float)iCellHeight), strFormat);
                            }
                            //Drawing Cells Borders 
                            e.Graphics.DrawRectangle(Pens.Black, new Rectangle((int)arrColumnLefts[iCount],
                                    iTopMargin, (int)arrColumnWidths[iCount], iCellHeight));

                            iCount++;
                        }
                    }
                    iRow++;
                    iTopMargin += iCellHeight;
                }

                //If more lines exist, print another page.
                if (bMorePagesToPrint)
                    e.HasMorePages = true;
                else
                    e.HasMorePages = false;
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #endregion


        private void exportxlsToolStripMenuItem_Click(object sender, EventArgs e)
        {

            this.saveFileDialog.Filter = "*.xlsx|*.xlsx|All files (*.*)|*.*";
            this.saveFileDialog.FilterIndex = 1;
            this.saveFileDialog.RestoreDirectory = true;
            this.saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
            this.saveFileDialog.Title = "Export to *.xlsx";
            this.saveFileDialog.ShowDialog();

            if (this.saveFileDialog.FileName != "")
            {

                // export all contests to excel
                // therfore use dummy DataGridView dv
                DataGridView dv = new DataGridView();
                dv.Hide();
                this.Controls.Add(dv);


                using (XLWorkbook wb = new XLWorkbook())
                {
                    foreach(string contest in toolStripComboBoxWettbewerb.Items)
                    {
                        // display data in datagridview
                        //dv.DataSource = getSeriesRunnerResults(contest);
                        dgv.DataSource = new DataView(_dtResults, "Wettbewerb = '" + contest + "' ", "type Desc", DataViewRowState.CurrentRows);



                        showDefaultToolStripMenuItem.PerformClick();

                        //// hide cloumns from view
                        //dv.Columns["Platz"].Visible = false;
                        //dv.Columns["Einlauf"].Visible = false;
                        //dv.Columns["Athleten-Nr."].Visible = false;
                        //dv.Columns["Wettbew-Nr."].Visible = false;
                        //dv.Columns["Wertungs-Gr"].Visible = false;
                        //dv.Columns["Altersklasse-Kennz."].Visible = false;
                        //dv.Columns["Wettbewerb"].Visible = false;
                        //dv.Columns["Wettbew-Name"].Visible = false;
                        //dv.Columns["Zeit ohne Text"].Visible = false;
                        //dv.Columns["m/w"].Visible = false;

                        //Creating DataTable
                        DataTable dt = DataGridView2DataTable(dv);
                        // forbidden excel charcters for workbook :\/?*[]

                        string tr = contest.Replace(@"/", string.Empty); //, ':', '\'','?','*','[',']'});
                        wb.Worksheets.Add(dt, contest.Replace(@"/", string.Empty)); 
                        
                    }


                    try
                    {
                        wb.SaveAs(this.saveFileDialog.FileName);
                    }
                    catch { }
                }
            }
        }


        private DataTable DataGridView2DataTable(DataGridView dgrid)
        {
            //Creating DataTable
            DataTable dt = new DataTable();

            //Adding the Columns
            foreach (DataGridViewColumn column in dgv.Columns)
            {
                dt.Columns.Add(column.HeaderText, column.ValueType);
            }

            //Adding the Rows
            foreach (DataGridViewRow row in dgv.Rows)
            {
                dt.Rows.Add();
                foreach (DataGridViewCell cell in row.Cells)
                {
                    dt.Rows[dt.Rows.Count - 1][cell.ColumnIndex] = cell.Value.ToString();
                }
            }
            return dt;
      
        }

        private void dgv_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {

            //if (dgv.Rows[e.RowIndex].Cells[dgv.Rows[e.RowIndex].Cells.Count - 1].ToString() == TimeSpan.Zero.ToString())
            //{
            //    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Red;
            //}

            if (dgv.Rows[e.RowIndex].Cells[dgv.Rows[e.RowIndex].Cells.Count - 1].Value.Equals(TimeSpan.Zero.ToString()))
            {
                dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.Red;
            }

        }

 
        
        // add row numbers ins DGV
        private void dgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridView dg = (DataGridView)sender;
            // Current row record
            string rowNumber = (e.RowIndex + 1).ToString();

            // Format row based on number of records displayed by using leading zeros
            while (rowNumber.Length < dg.RowCount.ToString().Length) rowNumber = "0" + rowNumber;

            // Position text
            SizeF size = e.Graphics.MeasureString(rowNumber, dg.RowHeadersDefaultCellStyle.Font);
            if (dg.RowHeadersWidth < (int)(size.Width + 20)) dg.RowHeadersWidth = (int)(size.Width + 20);

            // Use default system text brush
            Brush b = SystemBrushes.ControlText;


            // Draw row number
            e.Graphics.DrawString(rowNumber, dg.RowHeadersDefaultCellStyle.Font, b, e.RowBounds.Location.X + 15, e.RowBounds.Location.Y + ((e.RowBounds.Height - size.Height) / 2));
        }

        private void removeColoumnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //dgv.Columns.RemoveAt(_selected_Column);
            dgv.Columns[_selected_Column].Visible = false;
        }

        private void dgv_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                
                // work only if click was on headline
                if (e.RowIndex == -1 && e.ColumnIndex >=0 )
                {
                    _selected_Column = e.ColumnIndex;
                    dgv.ClearSelection();
                    dgv.Columns[_selected_Column].Selected = true;
                    contextMenuStripDGV.Show(Cursor.Position);                    
                }
            }
        }

        private void showAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewColumn col in dgv.Columns) {
                col.Visible = true;
            }
        }

        private void showDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewColumn col in dgv.Columns)
            {
                col.Visible = true;
            }

            // hide cloumns from view
            if (dgv.Columns.Contains("Zeit")) { dgv.Columns["Zeit"].Visible = false; }
            if (dgv.Columns.Contains("Platz")) { dgv.Columns["Platz"].Visible = false; }
            if (dgv.Columns.Contains("Einlauf")) { dgv.Columns["Einlauf"].Visible = false; }
            if (dgv.Columns.Contains("Athleten-Nr.")) { dgv.Columns["Athleten-Nr."].Visible = false; }
            if (dgv.Columns.Contains("Wettbew-Nr.")) { dgv.Columns["Wettbew-Nr."].Visible = false; }
            if (dgv.Columns.Contains("Wertungs-Gr")) { dgv.Columns["Wertungs-Gr"].Visible = false; }
            if (dgv.Columns.Contains("Altersklasse - Kennz.")) { dgv.Columns["Altersklasse-Kennz."].Visible = false; }
            if (dgv.Columns.Contains("Wettbewerb")) { dgv.Columns["Wettbewerb"].Visible = false; }
            if (dgv.Columns.Contains("Wettbew-Name")) { dgv.Columns["Wettbew-Name"].Visible = false; }
            if (dgv.Columns.Contains("Zeit ohne Text")) { dgv.Columns["Zeit ohne Text"].Visible = false; }
            if (dgv.Columns.Contains("m/w")) { dgv.Columns["m/w"].Visible = false; }
            if (dgv.Columns.Contains("LandesVerband")) { dgv.Columns["LandesVerband"].Visible = false; }
            if (dgv.Columns.Contains("Mannsch.-Nr.")) { dgv.Columns["Mannsch.-Nr."].Visible = false; }
            if (dgv.Columns.Contains("Starter_1")) { dgv.Columns["Starter_1"].Visible = false; }
            if (dgv.Columns.Contains("Starter_2")) { dgv.Columns["Starter_2"].Visible = false; }
            if (dgv.Columns.Contains("Starter_3")) { dgv.Columns["Starter_3"].Visible = false; }
            if (dgv.Columns.Contains("Starter_4")) { dgv.Columns["Starter_4"].Visible = false; }
            if (dgv.Columns.Contains("Starter_5")) { dgv.Columns["Starter_5"].Visible = false; }

        }

        private void toolStripComboBoxStartNo_SelectedIndexChanged(object sender, EventArgs e)
        {
            _dtResults = getSeriesRunnerResults();
            viewResultsDGV();

        }
    }
}
