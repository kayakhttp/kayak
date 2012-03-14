PRODUCT = "Kayak"
DESCRIPTION = "Kayak is an event-base IO libary for .NET. Kayak allows you to easily create TCP clients and servers, and contains an HTTP/1.1 server implementation."
VERSION = "0.7.2"
AUTHORS = "Benjamin van der Veen"
COPYRIGHT = "Copyright (c) 2007-2011 Benjamin van der Veen"
LICENSE_URL = "https://github.com/kayak/kayak/raw/HEAD/LICENSE"
PROJECT_URL = "https://github.com/kayak/kayak"

require 'albacore'
require 'uri'
require 'net/http'
require 'net/https'  

# Monkey patch Dir.exists? for Ruby 1.8.x
if RUBY_VERSION =~ /^1\.8/
  class Dir
    class << self
      def exists? (path)
        File.directory?(path)
      end
      alias_method :exist?, :exists?
    end
  end
end

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
        
def ensure_submodules()
  system("git submodule init")
  system("git submodule update")
end

def fetch(uri, limit = 10, &block)
  # We should choose a better exception.
  raise ArgumentError, 'too many HTTP redirects' if limit == 0

  http = Net::HTTP.new(uri.host, uri.port)
  if uri.scheme == "https"
    http.verify_mode = OpenSSL::SSL::VERIFY_PEER
    http.use_ssl = true
  end
  
  resp = http.request(Net::HTTP::Get.new(uri.request_uri)) { |response|
    case response
    when Net::HTTPRedirection then
      location = response['location']
      if block_given? then
        fetch(URI(location), limit - 1, &block)
      else
        fetch(URI(location), limit - 1)
      end
      return
    else
      response.read_body do |segment|
        yield segment
      end
      return
    end
  }
end

def rename_file(oldname, newname)
  # Ruby 1.8.7 on Mac sometimes reports File.exist? incorrectly
  # so work around this [not sure why that happens]
  begin
    File.delete(newname) 
  rescue => msg
    # File probably doesn't exist, if it does File.size? will work properly
    if File.size?(newname) != nil then
      fail "Failed to delete old file #{newname} with: #{msg}"
      raise
    end
  end     
  File.rename(oldname, newname)
end

def unpack_nuget_pkg(file, destination)
  unzip = Unzip.new
  unzip.destination = destination
  unzip.file = file
  unzip.execute
end
      
def ensure_nuget_package_nix(name) 
  # NuGet doesn't work on Mono. So we're going to download our dependencies from NuGet.org.
  
  zip_file = PACKAGES[name][:filename]
  tmp_file = "#{zip_file}.tmp"
  
  if File.exist?(zip_file) and File.size?(zip_file) != nil then
    puts "#{zip_file} already exists, skipping download"
    unpack_nuget_pkg(zip_file, PACKAGES[name][:folder])  
    return
  end
  
  puts "fetching #{zip_file}"
  File.open(tmp_file, "w") { |f| 
    uri = URI.parse(PACKAGES[name][:url])

    fetch(uri) do |seg| 
      f.write(seg)
    end
  }
                  
  if File.size?(tmp_file) == nil then
    fail "Download failed for #{zip_file}"
  end
  
  rename_file(tmp_file, zip_file)
  unpack_nuget_pkg(zip_file, PACKAGES[name][:folder])
end

def all_nuget_packages_present?()
  PACKAGES.values.each { |pkg| 
    if !Dir.exists? pkg[:folder] then
      puts "Package missing: #{pkg[:name]}"
      return false
    end
  }
  return true
end

def print_nuget_package_manifest()
  puts "Building with NuGet packages:"
  PACKAGES.values.each { |pkg| 
    puts "#{pkg[:name]} => #{pkg[:version]}"
  }
end

def ensure_all_nuget_packages_nix()
  PACKAGES.values.each { |pkg| 
    ensure_nuget_package_nix(pkg[:name])
  }
end

def read_package_config(filename)
  input_file = File.new(filename)
  xml = REXML::Document.new input_file
  
  xml.elements.each("packages/package") { |element| 
    yield element
  }
  
  input_file.close
end

def read_nuget_packages()          
  FileList["**/packages.config"].each { |config_file|
    read_package_config(config_file) { |pkg|
      name = pkg.attributes["id"]
      version = pkg.attributes["version"]   
      # puts "Read package #{name} with version #{version}"
      PACKAGES[name]={}
      PACKAGES[name][:name]=name
      PACKAGES[name][:version]=version
      PACKAGES[name][:folder]="#{PACKAGES_DIR}/#{name}.#{version}"
      PACKAGES[name][:filename]="#{PACKAGES_DIR}/#{name}.#{version}.nupkg" 
      PACKAGES[name][:url]="http://packages.nuget.org/api/v1/package/#{name}/#{version}"
    }
  }
end

def ensure_nuget_packages()
  Dir.mkdir PACKAGES_DIR unless Dir.exists? PACKAGES_DIR
  read_nuget_packages
  if all_nuget_packages_present?() then
    puts "All packages up to date"
    print_nuget_package_manifest
    return
  end
    
  if (is_nix()) then
    puts "updating packages with internal nuget replacement"
    ensure_all_nuget_packages_nix
    print_nuget_package_manifest
  else
    puts "updating packages with nuget"
    sh invoke_runtime("tools\\nuget.exe install Kayak.Tests\\packages.config -o #{PACKAGES_DIR}")
    print_nuget_package_manifest
  end
end

task :default => [:build, :test]
  
CONFIGURATION = "Release"
BUILD_DIR = File.expand_path("build")
OUTPUT_DIR = "#{BUILD_DIR}/out"
BIN_DIR = "#{BUILD_DIR}/bin"
PACKAGES_DIR = "packages"
PACKAGES = {}

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
  binaries = FileList("#{OUTPUT_DIR}/*.dll", "#{OUTPUT_DIR}/*.pdb") do |x|
    x.exclude(/nunit/)
    x.exclude(/.Tests/)
    x.exclude(/KayakExamples./)
  end
  FileUtils.cp_r binaries, BIN_DIR
end

task :dist_nuget => [:binaries, :build] do
  if is_nix()
    puts "Not running on Windows, skipping NuGet package creation."
  else 
    input_nuspec = "Kayak.nuspec"
    output_nuspec = "#{BUILD_DIR}/Kayak.nuspec"
    
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
    
    nuget = NuGetPack.new
    nuget.command = "tools/NuGet.exe"
    nuget.nuspec = output_nuspec
    nuget.output = BUILD_DIR
    #using base_folder throws as there are two options that begin with b in nuget 1.4
    nuget.parameters = "-Symbols"
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
  FileUtils.rm_rf "Kayak/bin"
  FileUtils.rm_rf "Kayak/obj"
  FileUtils.rm_rf "Kayak.Tests/bin"
  FileUtils.rm_rf "Kayak.Tests/obj"
end

task :dist_clean => [:clean] do
  FileUtils.rm_rf PACKAGES_DIR
end