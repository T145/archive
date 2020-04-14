desc 'Remove bootswatch themes'
task :clear_themes => :environment do
  sh 'rm -rf app/assets/javascripts/*'
  sh 'rm -rf app/assets/stylesheets/*'
end

desc 'Create bootswatch themes'
task :create_themes => :environment do
  sh 'rails g bootswatch:install default --force'

  THEMES.each do |theme|
    sh "rails g bootswatch:install #{theme} --force"
    sh "rails g bootswatch:import #{theme} --force"
  end
end