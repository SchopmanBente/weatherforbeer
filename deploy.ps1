Add-AzureRmAccount

Select-AzureRmSubscription -SubscriptionName "Azure for Students"

New-AzureRmResourceGroupDeployment -Name ProductionDeployment -ResourceGroupName serverside-beertime -TemplateFile "DeploymentTemplate.json"
