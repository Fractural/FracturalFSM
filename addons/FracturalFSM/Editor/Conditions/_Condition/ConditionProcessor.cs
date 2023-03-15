using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    /// <summary>
    /// Has creation methods for the condition editor and conditions. Is used as part of 
    /// a strategy pattern within TransitionEditor to create conditions + editors on the fly.
    /// </summary>
    public abstract class ConditionProcessor : Resource
    {
        /// <summary>
        /// ID is set by <see cref="TransitionEditor"/> when first initializing all the condition processors
        /// </summary>
        public int ID { get; set; }
        [Export]
        public PackedScene ConditionEditorPrefab { get; set; }
        public abstract bool CanHandle(Condition condition);
        public abstract Condition CreateConditionInstance();
        public abstract string ConditionName { get; }
    }

    [Tool]
    public abstract class ConditionProcessor<T> : ConditionProcessor
        where T : Condition, new()
    {
        public override bool CanHandle(Condition condition) => condition is T;
        public override Condition CreateConditionInstance() => CSharpScript<T>.New();
    }
}