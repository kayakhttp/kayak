using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using JimBlackler.DocsByReflection;
using System.IO;
using System.Runtime.CompilerServices;
using System.Web;
using MarkdownSharp;

namespace KayakDocs
{
    class Program
    {
        static Markdown markdown = new Markdown();
        static Assembly assembly;

        static string Transform(string s)
        {
            var tr = new StringReader(s);
            var sb = new StringBuilder();

            string line = null;
            while (!string.IsNullOrEmpty(line = tr.ReadLine()))
            {
                sb.AppendLine(line.Trim());
            }

            return markdown.Transform(sb.ToString());
        }

        static void Main(string[] args)
        {
            var assemblyToDocument = Path.GetFullPath(args[0]);

            var fileName = assemblyToDocument.Replace(".dll", "Docs.html");

            Console.WriteLine("Documenting assembly " + assemblyToDocument + " in file " + fileName);

            var assembly = Assembly.LoadFrom(assemblyToDocument);

            if (File.Exists(fileName))
                File.Delete(fileName);

            var file = File.Open(fileName, FileMode.Create);
            var tw = new StreamWriter(file);

            tw.WriteLine("<html><head><title>Kayak Documentation</title></head><body><h1>Kayak Documentation</h1>");

            var extensionType = assembly.GetTypes().Where(et => et.Name == "Extensions").First();

            var types = assembly.GetExportedTypes()
                .OrderBy(t => ((t.IsInterface ? "0" : "1") + "." + t.Name))
                .Where(t => t.Name != "Extensions" && !t.Namespace.Contains("LitJson"))
                .Select(t => new DocumentedType(extensionType, t));

            foreach (var type in types)
            {
                Console.WriteLine("Documenting type " + type.Type.Namespace + "." + type.Type.Name + "...");

                tw.WriteLine(string.Format("<h2>{0}</h2>", HttpUtility.HtmlEncode(DocumentedMember.GetFriendlyTypeName(type.Type))));
                tw.WriteLine(Transform(type.Description.Trim()));

                if (type.Constructors.Count > 0)
                    WriteMemberList(tw, type.Constructors, "Constructors");
                if (type.Properties.Count > 0)
                    WriteMemberList(tw, type.Properties, "Properties");
                if (type.Methods.Count > 0)
                    WriteMemberList(tw, type.Methods, "Methods");
                if (type.Extensions.Count > 0)
                    WriteMemberList(tw, type.Extensions, "Extensions");
                if (type.Fields.Count > 0)
                    WriteMemberList(tw, type.Fields, "Fields");
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
                tw.WriteLine(string.Format("<li><code>{0}</code> <div>{1}</div></li>", 
                    HttpUtility.HtmlEncode(property.Signature), Transform((property.Description ?? "No description available").Trim())));
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
        public List<DocumentedMember> Constructors;
        public List<DocumentedMember> Properties;
        public List<DocumentedMember> Methods;
        public List<DocumentedMember> Extensions;
        public List<DocumentedMember> Fields;

        public DocumentedType(Type extensionsType, Type type)
        {
            Type = type;
            Constructors = DocumentedMember.GetConstructors(type);
            Properties = DocumentedMember.GetProperties(type);
            Methods = DocumentedMember.GetMethods(type);
            
            if (extensionsType != null)
                Extensions = DocumentedMember.GetExtensions(extensionsType, type);

            if (type.IsValueType && !type.IsPrimitive) // is struct (?)
            {
                Fields = DocumentedMember.GetFields(type);
            }
            else
                Fields = new List<DocumentedMember>();
        }
    }

    class DocumentedMember
    {
        public string Signature;
        public string Description;

        public static List<DocumentedMember> GetConstructors(Type type)
        {
            var result = new List<DocumentedMember>();
            var constructors = type.GetConstructors().Where(c => c.GetParameters().Count() > 0);

            foreach (var constructor in constructors)
            {
                var documentedMember = new DocumentedMember();

                var parameters = constructor.GetParameters();

                documentedMember.Signature = string.Format("{0}({1})", GetFriendlyTypeName(type), GetParameterString(parameters));
                documentedMember.Description = GetDescription(constructor);

                result.Add(documentedMember);
            }

            return result;
        }

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

        public static List<DocumentedMember> GetExtensions(Type extensionsType, Type type)
        {
            var extensions = from method in extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                             where method.IsDefined(typeof(ExtensionAttribute), false)
                             where method.GetParameters()[0].ParameterType == type
                             select method;

            var result = new List<DocumentedMember>();

            foreach (var method in extensions)
            {
                var documentedMember = new DocumentedMember();

                documentedMember.Signature = GetSignature(method, method.GetParameters().Skip(1));
                documentedMember.Description = GetDescription(method);

                result.Add(documentedMember);
            }

            return result;
        }

        public static List<DocumentedMember> GetFields(Type type)
        {
            var result = new List<DocumentedMember>();

            foreach (var field in type.GetFields())
            {
                var documentedMember = new DocumentedMember();

                documentedMember.Signature = string.Format("{0} {1}", FixPrimitives(GetFriendlyTypeName(field.FieldType)), field.Name);
                documentedMember.Description = GetDescription(field);

                result.Add(documentedMember);
            }

            return result;
        }

        static string GetSignature(MethodInfo method, IEnumerable<ParameterInfo> parameters)
        {
            var returnType = FixPrimitives(GetFriendlyTypeName(method.ReturnType));
                

            return string.Format("{0} {1}({2})", returnType, method.Name, GetParameterString(parameters));
        }

        static string GetParameterString(IEnumerable<ParameterInfo> parameters)
        {
            var sb = new StringBuilder();

            bool first = true;
            foreach (var p in parameters)
            {
                if (!first)
                {
                    sb.Append(", ");
                }

                sb.Append(string.Format("{0} {1}", GetFriendlyTypeName(p.ParameterType), p.Name));
                first = false;
            }

            return FixPrimitives(sb.ToString());
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
            var xml = DocsByReflection.XMLFromMember(method);
            
            if (xml == null) return null;
            
            return xml["summary"].InnerText;
        }
    }
}
