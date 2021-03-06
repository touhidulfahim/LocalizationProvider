// Copyright (c) Valdis Iljuconoks. All rights reserved.
// Licensed under Apache-2.0. See the LICENSE file in the project root for more information

using System;
using DbLocalizationProvider.Abstractions;
using DbLocalizationProvider.Cache;
using DbLocalizationProvider.Commands;

namespace DbLocalizationProvider.Storage.SqlServer
{
    /// <summary>
    /// Implementation for creating or updating existing translation
    /// </summary>
    public class CreateOrUpdateTranslationHandler : ICommandHandler<CreateOrUpdateTranslation.Command>
    {
        /// <summary>
        /// Handles the command. Actual instance of the command being executed is passed-in as argument
        /// </summary>
        /// <param name="command">Actual command instance being executed</param>
        public void Execute(CreateOrUpdateTranslation.Command command)
        {
            var repository = new ResourceRepository();
            var resource = repository.GetByKey(command.Key);

            if(resource == null) return;

            var translation = resource.Translations.FindByLanguage(command.Language);
            if(translation == null)
            {
                var newTranslation = new LocalizationResourceTranslation
                {
                    Value = command.Translation,
                    Language = command.Language.Name,
                    ResourceId = resource.Id
                };

                repository.AddTranslation(resource, newTranslation);
            }
            else
            {
                translation.Value = command.Translation;
                repository.UpdateTranslation(resource, translation);
            }

            resource.ModificationDate = DateTime.UtcNow;
            resource.IsModified = true;

            repository.UpdateResource(resource);

            ConfigurationContext.Current.CacheManager.Remove(CacheKeyHelper.BuildKey(command.Key));
        }
    }
}
