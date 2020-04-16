using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace acme.net.Controllers
{
  [Route("[controller]")]
  [ApiController]
  public class newOrderController : ControllerBase
  {
    private readonly AcmeContext _context;
    public newOrderController(AcmeContext context)
    {
      _context = context;
    }
    // POST: newOrder
    [HttpPost]
    public Order Post([FromBody] AcmeJWT message)
    {
      if (message.validate(_context, out Account refAccount))
      {
        string payloadJson = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(message.encodedPayload);
        OrderList requests = Newtonsoft.Json.JsonConvert.DeserializeObject<OrderList>(payloadJson);

        DateTimeOffset expireTime = DateTime.UtcNow.AddDays(6);

        Order order = new Order()
        {
          orderID = generateID(),
          accountID = refAccount.accountID,
          status = Order.OrderStatus.pending,
          expires = expireTime
        };

        if (IISAppSettings.HasKey("Require-Identifier-PreAuth"))
        {
          foreach (OrderStub i in requests.identifiers)
          {
            IdentifierPreAuth ipa = _context.IdentifierPreAuth.Find(i.value);
            if (ipa is null)
            {
              order.error = new AcmeError()
              {
                type = AcmeError.ErrorType.userActionRequired,
                detail = "DNS name has not been authorized for ACME, please visit " + IISAppSettings.GetValue("Require-Identifier-PreAuth")
              };
              order.status = Order.OrderStatus.invalid;
              Response.StatusCode = 401;
              return order;
            }
          }
        }

     

        _context.Order.Add(order);

        List<string> retAuthz = new List<string>();
        foreach (OrderStub i in requests.identifiers)
        {
          Authorization newAuth = new Authorization()
          {
            authID = generateID(),
            orderID = order.orderID,
            status = Authorization.AuthorizationStatus.pending,
            expires = expireTime,
            identifier = i
          };
          _context.Authorization.Add(newAuth);
          retAuthz.Add(baseURL() + "authorization/" + newAuth.authID);
        }

        order.authorizations = retAuthz.ToArray();

        _context.SaveChanges();

        Response.StatusCode = 201;
        Response.Headers.Add("Location", baseURL() + "order/" + refAccount.accountID + "/" + order.orderID);
        Response.Headers.Add("Replay-Nonce", generateNonce());
        return order;
      }
      else
      {
        Response.StatusCode = 400;
        return null;
      }
    }
  }
}
