using System;

namespace Entities.Concrete.Dto
{
    public class ToggleFavoriteResponseDto
    {
        public bool IsFavorite { get; set; }
        public int FavoriteCount { get; set; }
    }
}
