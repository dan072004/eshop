global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using static eShop.Assistant.Tools.UnitTests.FakeHandler;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
