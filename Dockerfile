# Image chạy API (dùng cho Render và mọi môi trường container). Build: docker build -t sachluat-api .
# Cấu hình (connection string, JWT key, ...) truyền qua biến môi trường, không đưa vào image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy riêng file project trước để layer restore được cache khi chỉ sửa code.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/SachLuat.SharedKernel/SachLuat.SharedKernel.csproj src/SachLuat.SharedKernel/
COPY src/Modules/Auth/SachLuat.Modules.Auth/SachLuat.Modules.Auth.csproj src/Modules/Auth/SachLuat.Modules.Auth/
COPY src/Modules/Cart/SachLuat.Modules.Cart.Contracts/SachLuat.Modules.Cart.Contracts.csproj src/Modules/Cart/SachLuat.Modules.Cart.Contracts/
COPY src/Modules/Cart/SachLuat.Modules.Cart/SachLuat.Modules.Cart.csproj src/Modules/Cart/SachLuat.Modules.Cart/
COPY src/Modules/Catalog/SachLuat.Modules.Catalog.Contracts/SachLuat.Modules.Catalog.Contracts.csproj src/Modules/Catalog/SachLuat.Modules.Catalog.Contracts/
COPY src/Modules/Catalog/SachLuat.Modules.Catalog/SachLuat.Modules.Catalog.csproj src/Modules/Catalog/SachLuat.Modules.Catalog/
COPY src/Modules/Inventory/SachLuat.Modules.Inventory.Contracts/SachLuat.Modules.Inventory.Contracts.csproj src/Modules/Inventory/SachLuat.Modules.Inventory.Contracts/
COPY src/Modules/Inventory/SachLuat.Modules.Inventory/SachLuat.Modules.Inventory.csproj src/Modules/Inventory/SachLuat.Modules.Inventory/
COPY src/Modules/Ordering/SachLuat.Modules.Ordering/SachLuat.Modules.Ordering.csproj src/Modules/Ordering/SachLuat.Modules.Ordering/
COPY src/Modules/Content/SachLuat.Modules.Content/SachLuat.Modules.Content.csproj src/Modules/Content/SachLuat.Modules.Content/
COPY src/Modules/Shipping/SachLuat.Modules.Shipping.Contracts/SachLuat.Modules.Shipping.Contracts.csproj src/Modules/Shipping/SachLuat.Modules.Shipping.Contracts/
COPY src/Modules/Shipping/SachLuat.Modules.Shipping/SachLuat.Modules.Shipping.csproj src/Modules/Shipping/SachLuat.Modules.Shipping/
COPY src/Modules/Promotion/SachLuat.Modules.Promotion.Contracts/SachLuat.Modules.Promotion.Contracts.csproj src/Modules/Promotion/SachLuat.Modules.Promotion.Contracts/
COPY src/Modules/Promotion/SachLuat.Modules.Promotion/SachLuat.Modules.Promotion.csproj src/Modules/Promotion/SachLuat.Modules.Promotion/
COPY src/Modules/Notification/SachLuat.Modules.Notification.Contracts/SachLuat.Modules.Notification.Contracts.csproj src/Modules/Notification/SachLuat.Modules.Notification.Contracts/
COPY src/Modules/Notification/SachLuat.Modules.Notification/SachLuat.Modules.Notification.csproj src/Modules/Notification/SachLuat.Modules.Notification/
COPY src/SachLuat.Api/SachLuat.Api.csproj src/SachLuat.Api/
RUN dotnet restore src/SachLuat.Api/SachLuat.Api.csproj

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/SachLuat.Api/SachLuat.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render định tuyến tới cổng trong biến PORT; đặt PORT=8080 trên Render cho khớp.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "SachLuat.Api.dll"]
