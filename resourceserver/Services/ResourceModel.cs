using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
namespace AspireResourceServer.Services
{
    public partial class ResourceModel : ObservableObject, IEquatable<ResourceModel>
    {
        [ObservableProperty]
        private ImmutableDictionary<string, string> endpoints = ImmutableDictionary<string, string>.Empty;
        [ObservableProperty]
        private ImmutableDictionary<string, ResourceModel> references = ImmutableDictionary<string, ResourceModel>.Empty;
        [ObservableProperty]
        private ResourceModel parent = null;
        [ObservableProperty]
        private ImmutableDictionary<string, object> attributes = ImmutableDictionary<string, object>.Empty;
        [ObservableProperty]
        private ImmutableDictionary<string, string> environmentVariables = ImmutableDictionary<string, string>.Empty;
        public string Name { get; }
        public string IconName { get; }
        public Guid Guid { get; } = System.Guid.NewGuid();
        private ResourceModel(string name, string iconName)
        {
            Name = name;
            IconName = iconName;
        }

        public IEnumerable<ResourceModel> ReferencedBy()
        {
            yield break;
        }

        public void AddReferenceTo(ResourceModel destination)
        {
            if (!References.ContainsKey(destination.Name))
            {
                // destination.referencedBy.Add(this.Name, this);
                References = References.Add(destination.Name, destination);
            }
        }
        public void RemoveReferenceTo(ResourceModel destination)
        {
            if (References.ContainsKey(destination.Name))
            {
                // destination.referencedBy.Remove(this.Name);
                References = References.Remove(destination.Name);
            }
        }

        public static ResourceModel Create(string name, string icon = null)
        {
            return new ResourceModel(name, icon ?? "Box");
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ResourceModel);
        }

        public bool Equals(ResourceModel other)
        {
            if (other == null) return false;
            return String.Equals(Name, other.Name);
        }
    }
}