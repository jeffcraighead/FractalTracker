/* A simple class to handle CSV file.
 * (c) 2007 Hardono Arifanto
 * 
 */

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Data;
using System.IO;

namespace CsvDataSource
{
    public class CsvDataSource
    {
        private DataTable dt;

        //Constructor
        public CsvDataSource()
        {
            dt = new DataTable();
        }

        //Remove enclosing quotes
        private string RQ(string str)
        {
            str = str.Trim();
            if (str.StartsWith("\"") && str.EndsWith("\""))
                str = str.Substring(1, str.Length - 2);
            return str;
        }

        //Split a string using the specified separator character. Will keep value between quotes
        //as a single value
        private String[] CsvSplits(string data, char separatorChar)
        {
            string[] result;
            ArrayList ar = new ArrayList();
            int lastpost = 0;
            bool insideQuote = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == separatorChar)
                {
                    if (!insideQuote)
                    {
                        ar.Add(data.Substring(lastpost, i - lastpost));
                        lastpost = i + 1;
                    }

                    if (i > 0 && i < data.Length - 1)
                    {
                        if (data[i - 1] == '\"' && insideQuote)
                        {
                            ar.Add(data.Substring(lastpost, i - lastpost));
                            lastpost = i + 1;
                            insideQuote = false;
                        }
                        if (data[i + 1] == '\"' && !insideQuote)
                            insideQuote = true;
                    }
                }
            }
            ar.Add(data.Substring(lastpost, data.Length - lastpost));
            result = new string[ar.Count];
            for (int i = 0; i < ar.Count; i++)
                result[i] = ar[i].ToString();
            return result;
        }

        //Reading a File with Default Option: Without Column Name, and comma as separator character
        public DataTable ReadFile(string path)
        {            
            return ReadFile(path, false);
        }        

        //If WithColumnName is True, the first line of the CSV file will be treated as column names
        public DataTable ReadFile(string path, bool WithColumnName)
        {
            return ReadFile(path, WithColumnName, ',');
        }        

        public DataTable ReadFile(string path, bool WithColumnName, char SeparatorChar)
        {
            StreamReader sr = new StreamReader(path);
            dt = new DataTable();
            try
            {
                sr = new StreamReader(path);
                String line = sr.ReadLine();
                while (line.Trim() == "")
                    line = sr.ReadLine();
                String[] values = CsvSplits(line, SeparatorChar);

                if (WithColumnName)
                {
                    foreach (string val in values)
                    {
                        string val1 = RQ(val);
                        dt.Columns.Add(new DataColumn(val1));
                    }
                }
                else
                {
                    foreach (string val in values)
                    {
                        dt.Columns.Add(new DataColumn());
                    }
                    DataRow dr = dt.NewRow();
                    int counter = 0;
                    foreach (string val in values)
                    {
                        dr[counter++] = val;
                    }
                    dt.Rows.Add(dr);
                }
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    try
                    {
                        if (line.Trim() != "")
                        {
                            values = CsvSplits(line, SeparatorChar);
                            DataRow dr = dt.NewRow();
                            int counter = 0;
                            foreach (string val in values)
                            {
                                dr[counter++] = val;
                            }
                            dt.Rows.Add(dr);
                        }
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        System.Console.WriteLine("Error at row " + dt.Rows.Count);
                        System.Console.WriteLine(line + "\n");
                        System.Console.WriteLine(e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                sr.Close();
            }
            return dt;
        }

        public bool WriteFile(DataTable dtInput, string path)
        {
            DataTable tempdt = this.dt;
            bool result = false;
            this.dt = dtInput;
            result = WriteFile(path);
            this.dt = tempdt;
            return result;
        }

        public bool WriteFile(string path)
        {
            if (dt.Rows.Count > 0)
            {
                StreamWriter wr = new StreamWriter(path);
                string str = "";
                foreach (DataColumn dc in dt.Columns)
                {
                    if (dc.ColumnName != "")
                    {
                        str = str + "\"" + dc.ColumnName + "\",";
                    }
                    else
                        str = str + ",";
                }
                str = str.Substring(0, str.Length - 1); //remove the last comma
                wr.WriteLine(str);
                foreach (DataRow dr in dt.Rows)
                {
                    str = "";
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        str = str + "\"" + dr[i].ToString() +"\",";
                    }
                    str = str.Substring(0, str.Length - 1); //remove the last comma
                    wr.WriteLine(str);
                }
                wr.Close();
                return true;
            }
            else
                return false;
        }
    }
}