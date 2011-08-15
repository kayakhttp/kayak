PRODUCT = "Kayak"
DESCRIPTION = "Kayak is an event-base IO libary for .NET. Kayak allows you to easily create TCP clients and servers, and contains an HTTP/1.1 server implementation."
VERSION = "0.7.1"
AUTHORS = "Benjamin van der Veen"
COPYRIGHT = "Copyright (c) 2007-2011 Benjamin van der Veen"
LICENSE_URL = "https://github.com/kayak/kayak/raw/HEAD/LICENSE"
PROJECT_URL = "https://github.com/kayak/kayak"

require 'albacore'
require 'uri'
require 'net/http'

def is_nix
  !RUBY_PLATFORM.match("linux|darwin").nil?
end

def invoke_runtime(cmd)
  command = cmd
  if is_nix()
    command = "mono --runtime=v4.0 #{cmd}"
  end
  command
end

def transform_xml(input, output)
  input_file = File.new(input)
  xml = REXML::Document.new input_file
  
  yield xml
  
  input_file.close
  
  output_file = File.open(output, "w")
  formatter = REXML::Formatters::Default.new()
  formatter.write(xml, output_file)
  output_file.close
end

task :default => [:build, :test]
  
CONFIGURATION = "Release"
BUILD_DIR = File.expand_path("build")
OUTPUT_DIR = "#{BUILD_DIR}/out"
BIN_DIR = "#{BUILD_DIR}/bin"
NUGET_DIR = "#{BUILD_DIR}/nug"
PACKAGES_DIR = "packages"

assemblyinfo :assemblyinfo => :clean do |a|
  a.product_name = a.title = PRODUCT
  a.description = DESCRIPTION
  a.version = a.file_version = VERSION
  a.copyright = COPYRIGHT
  a.output_file = "Kayak/Properties/AssemblyInfo.cs"
  a.namespaces "System.Runtime.CompilerServices"
  a.custom_attributes :InternalsVisibleTo => "Kayak.Tests"
end

msbuild :build_msbuild do |b|
  b.properties :configuration => CONFIGURATION, "OutputPath" => OUTPUT_DIR
  b.targets :Build
  b.solution = "Kayak.sln"
end

xbuild :build_xbuild do |b|
  b.properties :configuration => CONFIGURATION, "OutputPath" => OUTPUT_DIR
  b.targets :Build
  b.solution = "Kayak.sln"
end

def ensure_submodules()
  system("git submodule init")
  system("git submodule update")
end

def ensure_nuget_packages_nix(name, version) 
  # NuGet doesn't work on Mono. So we're going to manually download our dependencies from NuGet.org.
  uri = URI.parse("http://packages.nuget.org/v1/Package/Download/#{name}/#{version}")

  zip_file = "#{PACKAGES_DIR}/#{name}.#{version}.nupkg"

  if File.exists? zip_file then
    puts "#{zip_file} already exists, skipping"
    return
  end
  
  puts "fetching #{zip_file}"
  f = open(zip_file, "w");
  begin
      Net::HTTP.start(uri.host, uri.port) do |http|
        http.request_get(uri.path) do |resp|
          resp.read_body do |segment|
            f.write(segment)
          end
        end
      end
  ensure
      f.close()
  end

  unzip = Unzip.new
  unzip.destination = "#{PACKAGES_DIR}/#{name}.#{version}"
  unzip.file = zip_file
  unzip.execute
end

def ensure_nuget_packages()
  Dir.mkdir "packages" unless Dir.exists? "packages"
  if (is_nix()) then
    ensure_nuget_packages_nix("NUnit", "2.5.10.11092")
  else
      puts "updating packages with nuget"
      sh invoke_runtime("tools\\nuget.exe install Kayak.Tests\\packages.config -o #{PACKAGES_DIR}")
      puts "done"
  end
end

task :build => :assemblyinfo do
  ensure_submodules()
  ensure_nuget_packages()
  build_task = is_nix() ? "build_xbuild" : "build_msbuild"
  Rake::Task[build_task].invoke
end

task :test => :build do
  nunit = invoke_runtime("packages/NUnit.2.5.10.11092/tools/nunit-console.exe")
  sh "#{nunit} -labels #{OUTPUT_DIR}/Kayak.Tests.dll"
end

task :binaries => :build do
  Dir.mkdir(BIN_DIR)
  binaries = FileList["#{OUTPUT_DIR}/*.dll", "#{OUTPUT_DIR}/*.pdb"]
    .exclude(/nunit/)
    .exclude(/.Tests/)
    .exclude(/.exe/)

  FileUtils.cp_r binaries, BIN_DIR
end

task :dist_nuget => [:binaries, :build] do
  if is_nix()
    puts "Not running on Windows, skipping NuGet package creation."
  else 
    Dir.mkdir(NUGET_DIR)

    input_nuspec = "Kayak.nuspec"
    output_nuspec = "#{NUGET_DIR}/Kayak.nuspec"
    
    transform_xml input_nuspec, output_nuspec do |x|
      x.root.elements["metadata/id"].text = PRODUCT
      x.root.elements["metadata/version"].text = VERSION
      x.root.elements["metadata/authors"].text = AUTHORS
      x.root.elements["metadata/owners"].text = AUTHORS
      x.root.elements["metadata/description"].text = DESCRIPTION
      x.root.elements["metadata/licenseUrl"].text = LICENSE_URL
      x.root.elements["metadata/projectUrl"].text = PROJECT_URL
      x.root.elements["metadata/tags"].text = "http io socket network async"
    end
    
    nuget_lib_dir = "#{NUGET_DIR}/lib"
    FileUtils.mkdir nuget_lib_dir
    FileUtils.cp_r FileList["#{BIN_DIR}/*"].exclude(/HttpMachine/), nuget_lib_dir
    
    nuget = NuGetPack.new
    nuget.command = "tools/NuGet.exe"
    nuget.nuspec = output_nuspec
    nuget.output = BUILD_DIR
    #using base_folder throws as there are two options that begin with b in nuget 1.4
    nuget.parameters = "-Symbols", "-BasePath \"#{NUGET_DIR}\""
    nuget.execute
  end
end

zip :dist_zip => [:build, :binaries] do |z|
  z.directories_to_zip BIN_DIR
  z.output_file = "kayak-#{VERSION}.zip"
  z.output_path = BUILD_DIR
end

task :dist => [:test, :dist_nuget, :dist_zip] do
end

task :clean do
  FileUtils.rm_rf BUILD_DIR
  FileUtils.rm_rf PACKAGES_DIR
  FileUtils.rm_rf "Kayak/bin"
  FileUtils.rm_rf "Kayak/obj"
  FileUtils.rm_rf "Kayak.Tests/bin"
  FileUtils.rm_rf "Kayak.Tests/obj"
end
