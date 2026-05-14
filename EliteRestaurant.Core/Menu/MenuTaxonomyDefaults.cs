namespace EliteRestaurant.Core.Menu;

public static class MenuTaxonomyDefaults
{
    /// <summary>Matches the original hard-coded EliteRestaurant menu structure.</summary>
    public static MenuTaxonomySettings CreateEliteDefault() => new()
    {
        Types =
        [
            new MenuTaxonomyType
            {
                Name = "Food",
                IsDrink = false,
                Sections =
                [
                    new MenuTaxonomySection
                    {
                        Name = "Starter/Appetizer",
                        Items = ["Starter/Appetizer"]
                    },
                    new MenuTaxonomySection
                    {
                        Name = "Main",
                        Items =
                        [
                            "Seafood", "Meat Meal", "Vegetarian", "Pasta", "Rice Dishes", "Grilled Meals", "Fast Food"
                        ]
                    },
                    new MenuTaxonomySection
                    {
                        Name = "Dessert",
                        Items = ["Dessert"]
                    }
                ]
            },
            new MenuTaxonomyType
            {
                Name = "Drink",
                IsDrink = true,
                Sections =
                [
                    new MenuTaxonomySection
                    {
                        Name = "Alcohol",
                        Items = ["Beer", "Champagne", "Cocktail", "Whisky"]
                    },
                    new MenuTaxonomySection
                    {
                        Name = "Non-Alcohol",
                        Items = ["Juice", "Mocktail", "Soft Drink", "Water"]
                    }
                ]
            }
        ]
    };
}
