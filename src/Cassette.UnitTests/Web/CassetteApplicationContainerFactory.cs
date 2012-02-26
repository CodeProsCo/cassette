﻿using System;
using System.IO;
using System.Linq;
using System.Web;
using Cassette.Configuration;
using Cassette.Manifests;
using Cassette.Scripts;
using Cassette.Stylesheets;
using Cassette.Utilities;
using Moq;
using Should;
using Xunit;

namespace Cassette.Web
{
    public class CassetteApplicationContainerFactory_Test
    {
        readonly CassetteConfigurationSection configurationSection;

        public CassetteApplicationContainerFactory_Test()
        {
            configurationSection = new CassetteConfigurationSection();            
        }

        [Fact]
        public void GivenStylesheetWithExternalReference_WhenCreateContainer_ThenExternalBundleAddedToBundleCollection()
        {
            using (var path = new TempDirectory())
            {
                Directory.CreateDirectory(Path.Combine(path, "styles"));
                File.WriteAllText(PathUtilities.Combine(path, "styles", "asset.css"), "/* @reference http://example.com */");

                var configuration = new StubConfiguration(bundles => bundles.Add<StylesheetBundle>("styles"));
                var factory = new CassetteApplicationContainerFactory(
                    new DelegateCassetteConfigurationFactory(() => new[] { configuration }),
                    new CassetteConfigurationSection(),
                    path,
                    "/",
                    false,
                    Mock.Of<HttpContextBase>
                );

                var container = factory.CreateContainer();

                container.Application.Bundles.Any(
                    b => b is ExternalStylesheetBundle && b.Path == "http://example.com"
                ).ShouldBeTrue();
            }
        }

        class StubConfiguration : ICassetteConfiguration
        {
            readonly Action<BundleCollection> configure;

            public StubConfiguration(Action<BundleCollection> configure)
            {
                this.configure = configure;
            }

            public void Configure(BundleCollection bundles, CassetteSettings settings)
            {
                configure(bundles);
            }
        }

        [Fact]
        public void WhenCreateContainer_ThenBundleIsLoadedFromManifest()
        {
            using (var path = new TempDirectory())
            {
                CompileTimeManifestWithBundleExists(path);

                var factory = CreateCassetteApplicationContainerFactory(path);
                var container = factory.CreateContainer();

                container.Application.FindBundleContainingPath<ScriptBundle>("~/test.js").ShouldNotBeNull();
            }
        }

        void CompileTimeManifestWithBundleExists(string rootDirectory)
        {
            var bundle = StubBundle();
            var bundleManifest = bundle.CreateBundleManifest(true);
            var cassetteManifest = new CassetteManifest("", new[] { bundleManifest });

            var manifestFilename = PathUtilities.Combine(rootDirectory, "App_Data", "cassette.xml");
            Directory.CreateDirectory(Path.Combine(rootDirectory, "App_Data"));
            using (var outputStream = File.Open(manifestFilename, FileMode.Create, FileAccess.Write))
            {
                var writer = new CassetteManifestWriter(outputStream);
                writer.Write(cassetteManifest);
            }

            configurationSection.PrecompiledManifest = "App_Data/cassette.xml";
        }

        ScriptBundle StubBundle()
        {
            var bundle = new ScriptBundle("~");
            bundle.Assets.Add(StubAsset("~/test.js"));
            bundle.Process(new CassetteSettings(""));
            bundle.Renderer = new ConstantHtmlRenderer<ScriptBundle>("");
            return bundle;
        }

        CassetteApplicationContainerFactory CreateCassetteApplicationContainerFactory(TempDirectory path)
        {
            return new CassetteApplicationContainerFactory(
                new DelegateCassetteConfigurationFactory(Enumerable.Empty<ICassetteConfiguration>),
                configurationSection,
                path,
                "/",
                false,
                Mock.Of<HttpContextBase>
            );
        }

        IAsset StubAsset(string filename)
        {
            var asset = new Mock<IAsset>();
            asset.Setup(a => a.OpenStream()).Returns(Stream.Null);
            asset.Setup(a => a.SourceFile.FullPath).Returns(filename);
            asset.Setup(a => a.Accept(It.IsAny<IBundleVisitor>())).Callback<IBundleVisitor>(v => v.Visit(asset.Object));
            return asset.Object;
        }
    }
}