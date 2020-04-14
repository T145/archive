# coding: utf-8
lib = File.expand_path('../lib', __FILE__)
$LOAD_PATH.unshift(lib) unless $LOAD_PATH.include?(lib)
require 'swissknife/version'

Gem::Specification.new do |spec|
  spec.name = 'swissknife'
  spec.version = SwissKnife::VERSION
  spec.authors = ['Taylor Shuler']
  spec.email = ['gnosoman@gmail.com']
  spec.summary = %q{A simple gem with sneaky commands!}
  spec.homepage = 'https://github.com/T145/swissknife'
  spec.license = 'MIT'

  spec.required_ruby_version = '>= 1.9.3'
  spec.files = `git ls-files -z`.split("\x0")
  spec.executables = Dir.entries 'bin'
  spec.test_files = spec.files.grep(%r{^(test|spec|features)/})
  spec.require_paths = ['lib']

  spec.add_development_dependency 'bundler', '~> 1.7'
  spec.add_development_dependency 'rake', '~> 10.0'
  spec.add_development_dependency 'rubocop', '~> 0.27.1'

  spec.add_dependency 'rubyzip', '~> 1.1.6'
  spec.add_dependency 'git', '~> 1.2.8'
  spec.add_dependency 'os', '~> 0.9.6'
end