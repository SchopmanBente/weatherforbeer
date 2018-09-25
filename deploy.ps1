Add-AzureRmAccount

Select-AzureRmSubscription -SubscriptionName "Azure for Students"

New-AzureRmResourceGroupDeployment -Name ProductionDeployment -ResourceGroupName WeatherForBeer -TemplateFile "DeploymentTemplate.json"
