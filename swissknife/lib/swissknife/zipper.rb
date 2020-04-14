require 'rubygems'
require 'zip'
require 'find'
require 'fileutils'

module SwissKnife
  class Zipper
    def self.zip(dir, destination, remove_after = false)
      Zip::ZipFile.open("#{destination}.zip", Zip::ZipFile::CREATE) do |zip|
        Find.find dir do |path|
          Find.prune if File.basename(path)[0] == ?.
          dest = /#{dir}\/(\w.*)/.match(path)
          # Skip files if they exists
          begin
            zip.add(dest[1], path) if dest
          rescue Zip::ZipEntryExistsError
            # ignored
          end
        end
      end
      FileUtils.rm_rf(dir) if remove_after
    end

    def self.unzip(zip, destination, remove_after = false)
      Zip::ZipFile.open zip do |subfile|
        subfile.each do |file|
          filepath = File.join destination, file.name
          FileUtils.mkdir_p File.dirname(filepath)
          subfile.extract file, filepath unless File.exist? filepath
        end
      end
      FileUtils.rm zip if remove_after
    end

    def self.open_one(zip, file)
      Zip::ZipFile.open(zip) do |subfile|
        subfile.each do |f|
          next unless f == file
          return f.get_input_stream.read
        end
      end
      nil
    end
  end
end