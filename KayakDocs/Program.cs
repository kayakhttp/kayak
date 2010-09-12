using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Kayak;
using JimBlackler.DocsByReflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Web;

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
                .OrderBy(t => (t.Namespace.Contains("Framework") ? "1" : "0") + "." + t.Name)
                .Where(t => t.Name != "Extensions" && !t.Namespace.Contains("LitJson"))
                .Select(t => new DocumentedType(t));

            foreach (var type in types)
            {
                Console.WriteLine("Documenting type " + type.Type.Namespace + "." + type.Type.Name + "...");

                tw.WriteLine(string.Format("<h2>{0}</h2>", HttpUtility.HtmlEncode(DocumentedMember.GetFriendlyTypeName(type.Type))));
                tw.WriteLine(string.Format("<p>{0}</p>", HttpUtility.HtmlEncode(type.Description)));

                if (type.Properties.Count > 0)
                    WriteMemberList(tw, type.Properties, "Properties");
                if (type.Methods.Count > 0)
                    WriteMemberList(tw, type.Methods, "Methods");
                if (type.Extensions.Count > 0)
                    WriteMemberList(tw, type.Extensions, "Extensions");
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
                tw.WriteLine(string.Format("<li><code>{0}</code> &mdash; {1}</li>", HttpUtility.HtmlEncode(property.Signature), HttpUtility.HtmlEncode(property.Description)));
            }

            tw.WriteLine("</ul>");
        }
    }

    class DocumentedType
    {
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

            foreach (var property in type.IsInterface ? type.GetProperties() : type.GetProperties(BindingFlags.DeclaredOnly))
            {
                var documentedMember = new DocumentedMember();

                documentedMember.Signature = string.Format("{0} {1}", FixPrimitives(GetFriendlyTypeName(property.PropertyType)), property.Name);
                documentedMember.Description = GetDescription(property);

                result.Add(documentedMember);
            }

            return result;
        }

        public static List<DocumentedMember> GetMethods(Type type)
        {
            var result = new List<DocumentedMember>();

            foreach (var method in type.IsInterface ? type.GetMethods() : type.GetMethods(BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName) continue;

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
            var paramsString = parameters.Aggregate("", (s, p) => string.Format("{0} {1}, ", GetFriendlyTypeName(p.ParameterType), p.Name)).TrimEnd(", ".ToCharArray());

            var returnType = FixPrimitives(GetFriendlyTypeName(method.ReturnType));
            paramsString = FixPrimitives(paramsString);
                

            return string.Format("{0} {1}({2})", returnType, method.Name, paramsString);
        }

        public static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            if (!type.IsGenericType)
            {
                return type.FullName;
            }

            var builder = new System.Text.StringBuilder();
            var name = type.Name;
            var index = name.IndexOf("`");
            builder.Append(name.Substring(0, index));
            builder.Append('<');
            var first = true;
            foreach (var arg in type.GetGenericArguments())
            {
                if (!first)
                {
                    builder.Append(", ");
                }
                builder.Append(GetFriendlyTypeName(arg));
                first = false;
            }
            builder.Append('>');
            return builder.ToString();
        }

        static string FixPrimitives(string s)
        {
            return s.Replace("System.String", "string")
                .Replace("System.Boolean", "bool")
                .Replace("System.Void", "void")
                .Replace("System.Byte", "byte")
                .Replace("System.Object", "object")
                .Replace("System.Int32", "int");
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
