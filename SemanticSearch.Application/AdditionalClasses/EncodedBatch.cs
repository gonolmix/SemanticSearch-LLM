using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticSearch.Application.AdditionalClasses
{
    public class EncodedBatch
    {
        public DenseTensor<int> InputIds { get; }
        public DenseTensor<int> AttentionMask { get; }
        public DenseTensor<int> TokenTypeIds { get; }

        public EncodedBatch(DenseTensor<int> inputIds, DenseTensor<int> attentionMask, DenseTensor<int> tokenTypeIds)
        {
            InputIds = inputIds;
            AttentionMask = attentionMask;
            TokenTypeIds = tokenTypeIds;
        }
    }
}
