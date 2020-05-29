using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Plugins
{
    public class ReportOnCreatePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
            (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.  
            if (context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity)
            {
                // Obtain the target entity from the input parameters.  
                Entity entity = (Entity)context.InputParameters["Target"];

                // Obtain the organization service reference which you will need for  
                // web service calls.  
                IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

                try
                {
                    // create the fetchxml to retrieve the observations
                    var fetchXmlObservations = @"
                        <fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='br_obervation'>
                            <attribute name='br_name' />
                            <filter type='and'>
                              <condition attribute='statecode' operator='eq' value='0' />
                            </filter>
                          </entity>
                        </fetch>";

                    // retrieve active observations using fetchXml passed into the organization service
                    var observations = service.RetrieveMultiple(new FetchExpression(fetchXmlObservations));

                    foreach (var observation in observations.Entities)
                    {
                        var reportObservation = new Entity("br_reportobservation");

                        reportObservation["br_observation"] = observation.GetAttributeValue<string>("br_name");
                        
                        // alternate way of doing the above line
                        //reportObservation["br_observation"] = observation["name"];

                        // we create the new entity reference to the report using the entity logical name and the record id from the context
                        reportObservation["br_report"] = new EntityReference(context.PrimaryEntityName, context.PrimaryEntityId);
                        
                        // alternate way of doing the above name
                        // reportObservation["br_report"] = new EntityReference("br_report", context.PrimaryEntityId);

                        // give the report observation record a name
                        reportObservation["br_name"] = observation.GetAttributeValue<string>("br_name");

                        // use the organization service to createthe reportobservation entity we just defined
                        var reportObservationGuid = service.Create(reportObservation);

                        // log our creation of the record to the tracing service
                        tracingService.Trace("New br_reportobservation created with id: " + reportObservationGuid);
                    }
                }

                catch (FaultException<OrganizationServiceFault> ex)
                {
                    // this message will show to the user in a pop up
                    throw new InvalidPluginExecutionException("An error occurred in ReportOnCreatePlugin.", ex);
                }

                catch (Exception ex)
                {
                    tracingService.Trace("FollowUpPlugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }
}
