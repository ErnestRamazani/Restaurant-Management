# Restaurant logo (customer website)

The **elite-menu** site loads the logo from the API: `GET /api/public/menu/assets/logo`.  
The API serves files from this folder **first** (see `RestaurantWebLogoResolver` in the API project).

## Drop-in file

Place **one** image here using a preferred name (checked in this order):

1. `restaurant-logo.svg`
2. `restaurant-logo.png`
3. `logo.svg`
4. `logo.png`

If none of those exist, the API uses the **first** image file in this folder (alphabetical order, common raster/vector extensions).

## After replacing the logo

- Run `dotnet build` on the solution so the logo is copied into `EliteRestaurant.Api` output.
- Restart the API if it is already running.

The database “cloud profile” logo is used only when **no** suitable file exists here (or the path cannot be resolved on the server).
