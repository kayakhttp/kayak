using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Kayak;
using JimBlackler.DocsByReflection;
using System.IO;
using System.Runtime.CompilerServices;

namespace KayakDocs
{
    class Program
    {
        static void Main(string[] args)
        {

            var fileName = "docs.html";

            if (File.Exists(fileName))
                File.Delete(fileName);

            var file = File.Open(fileName, FileMode.Create);
            var tw = new StreamWriter(file);

            tw.WriteLine("<html><head><title>Kayak Documentation</title></head><body><h1>Kayak Documentation</h1>");

            var types = Assembly.GetAssembly(typeof(IKayakContext)).GetExportedTypes()
                .OrderBy(t => t.Namespace + "." + t.Name)
                .Select(t => new DocumentedType(t));

            foreach (var type in types)
            {
                tw.WriteLine(string.Format("<h2>{0}</h2>", type.Name));
                tw.WriteLine(string.Format("<p>{0}</p>", type.Description));

                if (type.Properties.Count > 0)
                    WriteMemberList(tw, type.Properties, "Properties");
                if (type.Methods.Count > 0)
                    WriteMemberList(tw, type.Properties, "Methods");
                if (type.Extensions.Count > 0)
                    WriteMemberList(tw, type.Properties, "Extensions");
            }

            tw.WriteLine("</body></html>");

            tw.Flush();
            file.Flush();

            file.Close();
        }

        static void WriteMemberList(TextWriter tw, List<DocumentedMember> memberList, string title)
        {
            tw.WriteLine(string.Format("<h3>{0}</h3>", title));
            tw.WriteLine("<ul>");

            foreach (var property in memberList)
            {
                tw.WriteLine(string.Format("<li><code>{0}</code> &mdash; {1}</li>", property.Signature, property.Description));
            }

            tw.WriteLine("</ul>");
        }
    }

    class DocumentedType
    {
        public string Name { get { return Type.Name; } }
        public string Description
        {
            get
            {
                try
                {
                    return DocsByReflection.XMLFromType(Type)["summary"].InnerText;
                }
                catch
                {
                    return "No description available.";
                }
            }
        }

        public Type Type;
        public List<DocumentedMember> Properties;
        public List<DocumentedMember> Methods;
        public List<DocumentedMember> Extensions;

        public DocumentedType(Type type)
        {
            Type = type;
            Properties = DocumentedMember.GetProperties(type);
            Methods = DocumentedMember.GetMethods(type);
            Extensions = DocumentedMember.GetExtensions(type);
        }
    }

    class DocumentedMember
    {
        public string Signature;
        public string Description;

        public static List<DocumentedMember> GetProperties(Type type)
        {
            var result = new List<DocumentedMember>();

            foreach (var property in type.GetProperties(BindingFlags.DeclaredOnly))
            {
                var documentedMember = new DocumentedMember();

                documentedMember.Signature = string.Format("{0} {1}", property.PropertyType, property.Name);
                documentedMember.Description = GetDescription(property);

                result.Add(documentedMember);
            }

            return result;
        }

        public static List<DocumentedMember> GetMethods(Type type)
        {
            var result = new List<DocumentedMember>();

            foreach (var method in type.GetMethods())
            {
                var documentedMember = new DocumentedMember();

                documentedMember.Signature = GetSignature(method, method.GetParameters());
                documentedMember.Description = GetDescription(method);

                result.Add(documentedMember);
            }

            return result;
        }

        public static List<DocumentedMember> GetExtensions(Type type)
        {
            var extensions = from method in typeof(Extensions).GetMethods(BindingFlags.Static | BindingFlags.Public)
                             where method.IsDefined(typeof(ExtensionAttribute), false)
                             where method.GetParameters()[0].ParameterType == type
                             select method;

            var result = new List<DocumentedMember>();

            foreach (var method in extensions)
            {
                var documentedMember = new DocumentedMember();

                documentedMember.Signature = GetSignature(method, method.GetParameters());
                documentedMember.Description = GetDescription(method);

                result.Add(documentedMember);
            }

            return result;
        }

        static string GetSignature(MethodInfo method, IEnumerable<ParameterInfo> parameters)
        {
            var paramsString = parameters.Aggregate("", (s, p) => string.Format("{0} {1}, ", p.ParameterType, p.Name));

            return string.Format("{0} {1}({2})", method.ReturnType, method.Name, paramsString);
        }

        static string GetDescription(MemberInfo method)
        {
            try
            {
                return DocsByReflection.XMLFromMember(method)["summary"].InnerText;
            }
            catch
            {
                return "No description available.";
            }
        }
    }
}
