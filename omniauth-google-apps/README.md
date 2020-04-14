# OmniAuth Google Apps
This is the OmniAuth strategy for authenticating to Google Apps.

## Installation
Stick this into your application's Gemfile:
```ruby
gem 'omniauth-google-apps'
```
or this into your project's gemspec if your making a gem:
```ruby
# The "spec" variable depends on what Gem::Specification.new is set w/
spec.add_dependency 'omniauth-google-apps'
```
and then execute:
```bash
bundle --path vendor/bundle
```

## Usage
```ruby
# The domain attribute is optional
use OmniAuth::Builder {
  provider :google_apps #, domain: 'intridea.com'
}
```

## Contributing
1. Fork it
2. Commit your changes (`git commit -m "my awesome change!"`)
3. Create a new pull request
