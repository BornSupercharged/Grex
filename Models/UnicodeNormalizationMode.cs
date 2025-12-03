using System.Text;

namespace Grex.Models
{
    public enum UnicodeNormalizationMode
    {
        None = 0,
        FormC,
        FormD,
        FormKC,
        FormKD
    }

    public static class UnicodeNormalizationExtensions
    {
        public static NormalizationForm ToNormalizationForm(this UnicodeNormalizationMode mode)
        {
            return mode switch
            {
                UnicodeNormalizationMode.FormC => NormalizationForm.FormC,
                UnicodeNormalizationMode.FormD => NormalizationForm.FormD,
                UnicodeNormalizationMode.FormKC => NormalizationForm.FormKC,
                UnicodeNormalizationMode.FormKD => NormalizationForm.FormKD,
                _ => NormalizationForm.FormC // Default to FormC
            };
        }
    }
}