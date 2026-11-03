global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Net.Http;
global using System.Threading.Tasks;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.TestHost;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Hosting;
global using Xunit;

// Each test class starts its own Postgres through Aspire; starting two orchestrators at once can time out.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
