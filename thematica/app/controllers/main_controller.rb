class MainController < ApplicationController
  def default
    render :default
  end

  THEMES.each { |theme| define_method(theme) { render :default } }
end
