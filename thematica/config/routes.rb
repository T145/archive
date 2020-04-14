Rails.application.routes.draw do
  get 'main/default'

  THEMES.each { |theme| match theme, to: "main##{theme}", via: :get }

  root 'main#default'
end
