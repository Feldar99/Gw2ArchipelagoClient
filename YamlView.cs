using Blish_HUD.Controls;
using Blish_HUD.Graphics.UI;
using Gw2Sharp.WebApi.V2.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gw2Archipelago
{
    internal class YamlView : View
    {
        private Module module;
        private List<TrackBar> characterSliders;
        private List<Label> characterLabels;

        private IReadOnlyList<Character> characters;

        public YamlView(Module module)
        {
            this.module = module;
        }



        protected override void Build(Container buildPanel)
        {
            base.Build(buildPanel);
                foreach (var characterName in .)
                {

                }
            }
        }

        protected override async Task<bool> Load(IProgress<string> progress)
        {

            if (module.Gw2ApiManager.HasPermission(Gw2Sharp.WebApi.V2.Models.TokenPermission.Characters))
            {
                characters = await module.Gw2ApiManager.Gw2ApiClient.V2.Characters.AllAsync();
            }

            return base.Load(progress);
        }
    }
}