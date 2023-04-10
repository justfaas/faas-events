using k8s;
using k8s.Models;

/*
A "minified" version of a Function with the Spec stripped off.
This is used essentially to map the apiVersion and kind with the controller,
since for topic mapping we only need the annotations in the metadata.
*/

[KubernetesEntity( Group = "justfaas.com", ApiVersion = "v1alpha1", Kind = "Function", PluralName = "functions" )]
internal sealed class V1Alpha1Function : IKubernetesObject, IMetadata<V1ObjectMeta>
{
    public string ApiVersion { get; set; } = "v1alpha1";
    public string Kind { get; set; } = "Function";
    public V1ObjectMeta Metadata { get; set; } = new V1ObjectMeta();

    public string NamespacedName() => string.Join( '/', this.Namespace(), this.Name() );
}
