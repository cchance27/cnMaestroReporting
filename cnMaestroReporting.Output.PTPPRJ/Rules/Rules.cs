namespace cnMaestroReporting.Output.PTPPRJ
{
    public static class Rules
    {
        public struct RuleAttributeSet
        {
            public string name;
            public string format;
            public string stop_execution;
            public string disabled;
            public string hidesm;
            public string boolean;
            public string excluded;
            public string hidden;
            public string format_settings;
            public string description;
            public RuleExpressionGroup[] expressionGroups;
        }

        public struct RuleExpressionGroup
        {
            public string boolean;
            public RuleExpression[] expressions;
        }

        public struct RuleExpression
        {
            public string comparison_value;
            public string predicate;
            public string property;
        }

    }
}