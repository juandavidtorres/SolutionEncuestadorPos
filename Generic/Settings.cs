//Development by Kelmer Ashley Comas Cardona © 2014

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Windows.Forms;
using System.Xml;
using System.Reflection;
using System.IO;


namespace Generic
{
    public class Settings
    {
        private XmlDocument appConfig = new XmlDocument();
        private string[] sectionGroupNames = { "applicationSettings", "userSettings", "connectionStrings" };

        public Settings()
        {
            string path = String.Format("{0}\\{1}", this.AppPath(), this.LibraryName());
            this.Path = path + ".config";
            this.appConfig.Load(this.Path);
        }

        public Settings(string path)
        {
            this.Path = path + ".config";
            this.appConfig.Load(this.Path);
        }

        public string Path { get; set; }

        //OBTINE EL ELEMENTO ESPECIFICADO POR LA CLAVE DADA EN LAS SECCIONES ESPECIFICADAS COMO PARAMETRO
        private XmlNode GetElement(string key, string[] sectionGroupNames)
        {
            try
            {
                string sectionName = "", valueKey = "";
                XmlNode sectionGroupNode, sectionNode;
                XmlNodeList settingNodes = null;

                foreach (string sectionGroupName in sectionGroupNames)
                {
                    if (sectionGroupName != "connectionStrings" && !String.IsNullOrEmpty(sectionGroupName))
                    {
                        sectionGroupNode = appConfig.SelectSingleNode("descendant::" + sectionGroupName);

                        if (sectionGroupNode != null)
                        {
                            if (sectionGroupNode.FirstChild != null)
                                sectionName = sectionGroupNode.FirstChild.Name;

                            sectionNode = sectionGroupNode.SelectSingleNode("descendant::" + sectionName);

                            if (sectionNode != null) settingNodes = sectionNode.ChildNodes;
                        }
                    }
                    else
                    {
                        sectionNode = appConfig.SelectSingleNode("descendant::" + sectionGroupName);
                        if (sectionNode != null) settingNodes = sectionNode.ChildNodes;
                    }

                    if (settingNodes != null)
                        foreach (XmlNode node in settingNodes)
                        {
                            valueKey = node.Attributes["name"].Value;
                            if (valueKey.ToUpper() == key.ToUpper()) return node;
                        }
                }
            }
            catch { throw; }

            return null;
        }

        //OBTIENE EL VALOR DE LA CONFIGURACION ESPECIFICADA
        public string GetValue(string key)
        {
            try
            {
                XmlNode item = this.GetElement(key, sectionGroupNames);

                if (item != null)
                {
                    if (item.ParentNode.Name == "connectionStrings")
                        return item.Attributes["connectionString"].Value;
                    else return item.InnerText;
                }
            }
            catch { throw; }

            return null;
        }

        //ESTABLECE EL VALOR DE LA CONFIGURACION ESPECIFICADA
        public void SetValue(string key, string value)
        {
            try
            {
                XmlNode item = this.GetElement(key, sectionGroupNames);

                if (item != null)
                {
                    if (item.ParentNode.Name == "connectionStrings")
                        item.Attributes["connectionString"].Value = value;
                    else item.InnerText = value;


                    appConfig.Save(this.Path);
                }
            }
            catch { throw; }
        }


        private string AppPath()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return System.IO.Path.GetDirectoryName(assembly.GetModules()[0].FullyQualifiedName);
        }

        public string LibraryName()
        {
            Assembly assembly = Assembly.GetCallingAssembly();

            string LibraryName = assembly.GetModules()[0].Name;
            return LibraryName;
        }
    }
}
