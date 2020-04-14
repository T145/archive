require 'openid/consumer'
require 'gapps_openid'

module OpenID
  # Because gapps_openid changes the discovery order (looking first for Google Apps, then anything else), we need to patch it to make it play nicely with others.
  def self.discover(uri)
    raise OpenID::DiscoveryFailure.new('No URL Provided', 422) if uri.nil?

    begin
      discovered = self.default_discover uri

      if discovered.last.empty?
        info = discover_google_apps uri
        return info if info
      end

      return discovered
    rescue OpenID::DiscoveryFailure => e
      info = discover_google_apps uri
      if info.nil?
        raise e
      else
        return info
      end
    end
  end

  def self.discover_google_apps(uri)
    GoogleDiscovery.new.perform_discovery uri
  end
end

module OmniAuth
  module Strategies
    class GoogleApps < OmniAuth::Strategies::OpenID
      option :name, 'google_apps'
      option :domain, nil

      def get_identifier
        OmniAuth::Form.new(title: 'Google Apps Authentication').label_field('Google Apps Domain', 'domain').input_field('url', 'domain').to_response
      end

      def identifier
        options[:domain] || request['domain']
      end
    end
  end
end
